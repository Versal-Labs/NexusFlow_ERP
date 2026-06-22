using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Constants;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Commands
{
    public record DepositChequeCommand(int ChequeId, int BankAccountId, DateTime DepositDate) : IRequest<Result<int>>, IFinancialPeriodControlledRequest
    {
        public DateTime FinancialDate => DepositDate;
    }

    public class DepositChequeHandler : IRequestHandler<DepositChequeCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly IFinancialAccountResolver _accountResolver;

        public DepositChequeHandler(IErpDbContext context, IJournalService journalService, IFinancialAccountResolver accountResolver)
        {
            _context = context; _journalService = journalService; _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(DepositChequeCommand request, CancellationToken cancellationToken)
        {
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                var cheque = await _context.ChequeRegisters.Include(c => c.Customer)
                    .FirstOrDefaultAsync(c => c.Id == request.ChequeId, cancellationToken);

                if (cheque == null) return Result<int>.Failure("Cheque not found.");
                if (cheque.Status != ChequeStatus.InSafe) return Result<int>.Failure($"Cannot deposit a cheque with status: {cheque.Status}");

                // Update Cheque Status
                cheque.Status = ChequeStatus.Deposited;
                cheque.DepositedBankAccountId = request.BankAccountId;

                // GL POSTING: Move money from Safe to Bank
                int undepositedFundsId = await _accountResolver.ResolveAccountIdAsync("Account.Asset.UndepositedFunds", cancellationToken);

                var journalLines = new List<JournalLineRequest>
                {
                    new JournalLineRequest { AccountId = request.BankAccountId, Debit = cheque.Amount, Credit = 0, Note = $"Cheque Deposit {cheque.ChequeNumber}" },
                    new JournalLineRequest { AccountId = undepositedFundsId, Debit = 0, Credit = cheque.Amount, Note = $"Clear Safe {cheque.ChequeNumber}" }
                };

                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = request.DepositDate,
                    Description = $"Cheque Deposit: {cheque.ChequeNumber} ({cheque.Customer.Name})",
                    Module = "Treasury",
                    ReferenceNo = $"DEP-{cheque.ChequeNumber}",
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception(jResult.Message);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(cheque.Id, "Cheque deposited successfully.");
            }
            catch (Exception ex) { await transaction.RollbackAsync(cancellationToken); return Result<int>.Failure(ex.Message); }
        }
    }

    // ==========================================
    // 2. BOUNCE CHEQUE
    // ==========================================
    public sealed class BounceChequeCommand : IRequest<Result<int>>, IFinancialPeriodControlledRequest
    {
        public int ChequeId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime BounceDate { get; set; }
        public decimal BankFeeAmount { get; set; }
        public int? FeeSourceAccountId { get; set; }
        public decimal RecoverableFromCustomerAmount { get; set; }
        public DateTime FinancialDate => BounceDate;
    }

    public class BounceChequeHandler : IRequestHandler<BounceChequeCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly IFinancialAccountResolver _accountResolver;
        private readonly INumberSequenceService _sequenceService;

        public BounceChequeHandler(IErpDbContext context, IJournalService journalService, IFinancialAccountResolver accountResolver, INumberSequenceService sequenceService)
        {
            _context = context; _journalService = journalService; _accountResolver = accountResolver; _sequenceService = sequenceService;
        }

        public async Task<Result<int>> Handle(BounceChequeCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason)) return Result<int>.Failure("Dishonor reason is required.");
            if (request.BankFeeAmount < 0 || request.RecoverableFromCustomerAmount < 0)
                return Result<int>.Failure("Dishonor fee amounts cannot be negative.");
            if (request.RecoverableFromCustomerAmount > request.BankFeeAmount)
                return Result<int>.Failure("Customer recoverable amount cannot exceed the bank dishonor fee.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                var cheque = await _context.ChequeRegisters
                    .Include(c => c.Customer)
                    .Include(c => c.OriginalReceipt)
                        .ThenInclude(r => r.Allocations)
                            .ThenInclude(a => a.SalesInvoice)
                    .Include(c => c.EndorsedPayment)
                        .ThenInclude(p => p.Allocations)
                            .ThenInclude(a => a.SupplierBill)
                    .FirstOrDefaultAsync(c => c.Id == request.ChequeId, cancellationToken);

                if (cheque == null) return Result<int>.Failure("Cheque not found.");

                if (cheque.Status == ChequeStatus.Cleared || cheque.Status == ChequeStatus.Bounced)
                    return Result<int>.Failure($"Cannot bounce a cheque that is currently marked as {cheque.Status}.");

                int creditAccountId;
                string stateNote;

                // ==========================================
                // 2. DETERMINE THE STATE OF THE CHEQUE
                // ==========================================
                if (cheque.Status == ChequeStatus.InSafe)
                {
                    creditAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Asset.UndepositedFunds", cancellationToken);
                    stateNote = "In Safe";
                }
                else if (cheque.Status == ChequeStatus.Deposited)
                {
                    if (!cheque.DepositedBankAccountId.HasValue) return Result<int>.Failure("Data corruption: Deposited cheque has no linked bank account.");
                    creditAccountId = cheque.DepositedBankAccountId.Value;
                    stateNote = "Deposited";
                }
                else if (cheque.Status == ChequeStatus.Endorsed)
                {
                    if (cheque.EndorsedPayment == null || !cheque.EndorsedPayment.SupplierId.HasValue)
                        return Result<int>.Failure("Data corruption: Endorsed cheque has no linked supplier payment.");

                    var supplier = await _context.Suppliers.FindAsync(new object[] { cheque.EndorsedPayment.SupplierId.Value }, cancellationToken);
                    if (supplier == null || supplier.DefaultPayableAccountId == 0) return Result<int>.Failure("Supplier AP Account mapping missing.");

                    creditAccountId = supplier.DefaultPayableAccountId ?? 0;
                    stateNote = "Endorsed";

                    cheque.EndorsedPayment.IsVoided = true;
                    cheque.EndorsedPayment.VoidedAt = DateTime.UtcNow;
                    cheque.EndorsedPayment.VoidReason = $"Customer cheque {cheque.ChequeNumber} dishonored: {request.Reason}";
                }
                else return Result<int>.Failure($"Cheque status {cheque.Status} cannot be dishonored.");

                var reversalReference = $"BNC-{cheque.Id}-{request.BounceDate:yyyyMMdd}";
                cheque.OriginalReceipt.IsVoided = true;
                cheque.OriginalReceipt.VoidedAt = DateTime.UtcNow;
                cheque.OriginalReceipt.VoidReason = $"Cheque {cheque.ChequeNumber} dishonored: {request.Reason}";
                cheque.OriginalReceipt.ReversalReferenceNo = reversalReference;
                if (cheque.EndorsedPayment != null) cheque.EndorsedPayment.ReversalReferenceNo = reversalReference;

                var invoiceIds = cheque.OriginalReceipt.Allocations.Where(a => a.SalesInvoiceId.HasValue)
                    .Select(a => a.SalesInvoiceId!.Value).Distinct().ToList();
                foreach (var alloc in cheque.OriginalReceipt.Allocations)
                {
                    if (alloc.SalesInvoice != null)
                    {
                        var invoice = alloc.SalesInvoice;
                        invoice.AmountPaid = await _context.PaymentAllocations
                            .Where(a => a.SalesInvoiceId == invoice.Id
                                && a.PaymentTransactionId != cheque.OriginalReceiptId
                                && !a.PaymentTransaction.IsVoided)
                            .SumAsync(a => (decimal?)a.AmountAllocated, cancellationToken) ?? 0m;
                        invoice.PaymentStatus = invoice.AmountPaid <= 0 ? InvoicePaymentStatus.Unpaid : InvoicePaymentStatus.Partial;
                    }
                }

                if (cheque.EndorsedPayment != null)
                {
                    foreach (var alloc in cheque.EndorsedPayment.Allocations.Where(a => a.SupplierBill != null))
                    {
                        var bill = alloc.SupplierBill!;
                        bill.AmountPaid = await _context.PaymentAllocations
                            .Where(a => a.SupplierBillId == bill.Id
                                && a.PaymentTransactionId != cheque.EndorsedPayment.Id
                                && !a.PaymentTransaction.IsVoided)
                            .SumAsync(a => (decimal?)a.AmountAllocated, cancellationToken) ?? 0m;
                        bill.PaymentStatus = bill.AmountPaid <= 0 ? InvoicePaymentStatus.Unpaid : InvoicePaymentStatus.Partial;
                    }
                }

                var commissions = await _context.CommissionLedgers.Where(c => invoiceIds.Contains(c.SalesInvoiceId)).ToListAsync(cancellationToken);
                foreach (var commission in commissions.Where(x => x.CommissionAmount > 0))
                {
                    if (commission.Status == CommissionStatus.Paid)
                    {
                        _context.CommissionLedgers.Add(new CommissionLedger
                        {
                            SalesRepId = commission.SalesRepId,
                            SalesInvoiceId = commission.SalesInvoiceId,
                            CommissionAmount = -commission.CommissionAmount,
                            Status = CommissionStatus.ReadyToPay
                        });
                    }
                    else
                    {
                        commission.Status = CommissionStatus.Unearned;
                    }
                }

                var journalLines = new List<JournalLineRequest>
                {
                    new JournalLineRequest { AccountId = cheque.Customer.DefaultReceivableAccountId, Debit = cheque.Amount, Credit = 0, Note = $"Bounced Cheque {cheque.ChequeNumber}" },
                    new JournalLineRequest { AccountId = creditAccountId, Debit = 0, Credit = cheque.Amount, Note = $"Bounce Reversal ({stateNote})" }
                };

                if (request.BankFeeAmount > 0)
                {
                    var feeSourceAccountId = request.FeeSourceAccountId ?? cheque.DepositedBankAccountId;
                    if (!feeSourceAccountId.HasValue)
                        return Result<int>.Failure("Select the bank or cash account charged with the dishonor fee.");

                    var bankFeesAccountId = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.BankFees, cancellationToken);
                    journalLines.Add(new JournalLineRequest { AccountId = bankFeesAccountId, Debit = request.BankFeeAmount, Credit = 0, Note = "Bank dishonor fee" });
                    journalLines.Add(new JournalLineRequest { AccountId = feeSourceAccountId.Value, Debit = 0, Credit = request.BankFeeAmount, Note = "Bank dishonor fee charged" });

                    if (request.RecoverableFromCustomerAmount > 0)
                    {
                        var memoNo = await _sequenceService.GenerateNextNumberAsync(NumberSequenceKeys.CustomerDebitMemo, cancellationToken);
                        _context.CustomerDebitMemos.Add(new CustomerDebitMemo
                        {
                            DebitMemoNumber = memoNo,
                            Date = request.BounceDate,
                            CustomerId = cheque.CustomerId,
                            ChequeRegisterId = cheque.Id,
                            Amount = request.RecoverableFromCustomerAmount,
                            Reason = $"Recoverable dishonor charge for cheque {cheque.ChequeNumber}"
                        });
                        journalLines.Add(new JournalLineRequest { AccountId = cheque.Customer.DefaultReceivableAccountId, Debit = request.RecoverableFromCustomerAmount, Credit = 0, Note = memoNo });
                        journalLines.Add(new JournalLineRequest { AccountId = bankFeesAccountId, Debit = 0, Credit = request.RecoverableFromCustomerAmount, Note = "Customer-recoverable dishonor fee" });
                    }
                }

                // Accounting invariant: this new journal reverses the original asset/AP settlement without rewriting closed history.
                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = request.BounceDate,
                    Description = $"Dishonored Cheque: {cheque.ChequeNumber} - {request.Reason}",
                    Module = "Treasury",
                    ReferenceNo = reversalReference,
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception($"GL Reversal Failed: {jResult.Message}");

                cheque.Status = ChequeStatus.Bounced;
                cheque.BounceReason = request.Reason;
                cheque.DishonoredDate = request.BounceDate;
                cheque.ReversalReferenceNo = reversalReference;

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(cheque.Id, "Cheque marked as Bounced. GL reversed, Supplier Bills reinstated, and Sales Invoices reinstated.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Bounce Operation Failed: {ex.Message}");
            }
        }
    }
}
