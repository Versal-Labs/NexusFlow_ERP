using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Commands
{
    public record DepositChequeCommand(int ChequeId, int BankAccountId, DateTime DepositDate) : IRequest<Result<int>>;

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
    public record BounceChequeCommand(int ChequeId, string Reason, DateTime BounceDate) : IRequest<Result<int>>;

    public class BounceChequeHandler : IRequestHandler<BounceChequeCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly IFinancialAccountResolver _accountResolver;

        public BounceChequeHandler(IErpDbContext context, IJournalService journalService, IFinancialAccountResolver accountResolver)
        {
            _context = context; _journalService = journalService; _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(BounceChequeCommand request, CancellationToken cancellationToken)
        {
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                // ==========================================
                // 1. DEEP FETCH THE CHEQUE GRAPH (Fixed .Includes)
                // ==========================================
                var cheque = await _context.ChequeRegisters
                    .Include(c => c.Customer)
                    // Fetch Customer AR Side
                    .Include(c => c.OriginalReceipt)
                        .ThenInclude(r => r.Allocations)
                            .ThenInclude(a => a.SalesInvoice)
                    // Fetch Supplier AP Side (THIS FIXES YOUR BUG!)
                    .Include(c => c.EndorsedPayment)
                        .ThenInclude(p => p.Allocations)
                            .ThenInclude(a => a.SupplierBill)
                    .FirstOrDefaultAsync(c => c.Id == request.ChequeId, cancellationToken);

                if (cheque == null) return Result<int>.Failure("Cheque not found.");

                if (cheque.Status == ChequeStatus.Cleared || cheque.Status == ChequeStatus.Bounced)
                    return Result<int>.Failure($"Cannot bounce a cheque that is currently marked as {cheque.Status}.");

                int creditAccountId = 0;
                string stateNote = "";

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
                    // SCENARIO: The cheque was given to a Supplier
                    if (cheque.EndorsedPayment == null || !cheque.EndorsedPayment.SupplierId.HasValue)
                        return Result<int>.Failure("Data corruption: Endorsed cheque has no linked supplier payment.");

                    var supplier = await _context.Suppliers.FindAsync(new object[] { cheque.EndorsedPayment.SupplierId.Value }, cancellationToken);
                    if (supplier == null || supplier.DefaultPayableAccountId == 0) return Result<int>.Failure("Supplier AP Account mapping missing.");

                    creditAccountId = supplier.DefaultPayableAccountId ?? 0;
                    stateNote = "Endorsed";

                    // Void the Supplier Payment transaction
                    cheque.EndorsedPayment.IsVoided = true;

                    // Reinstate the Supplier Bills (This loop now works perfectly!)
                    foreach (var alloc in cheque.EndorsedPayment.Allocations)
                    {
                        if (alloc.SupplierBill != null)
                        {
                            var bill = alloc.SupplierBill;
                            bill.AmountPaid -= alloc.AmountAllocated;
                            bill.PaymentStatus = bill.AmountPaid <= 0 ? InvoicePaymentStatus.Unpaid : InvoicePaymentStatus.Partial;
                        }
                    }
                }

                // ==========================================
                // 3. REINSTATE CUSTOMER INVOICES & COMMISSIONS
                // ==========================================
                var invoiceIds = cheque.OriginalReceipt.Allocations.Select(a => a.SalesInvoiceId).ToList();
                var commissions = await _context.CommissionLedgers.Where(c => invoiceIds.Contains(c.SalesInvoiceId)).ToListAsync(cancellationToken);

                foreach (var alloc in cheque.OriginalReceipt.Allocations)
                {
                    if (alloc.SalesInvoice != null)
                    {
                        var invoice = alloc.SalesInvoice;
                        invoice.AmountPaid -= alloc.AmountAllocated;
                        invoice.PaymentStatus = invoice.AmountPaid <= 0 ? InvoicePaymentStatus.Unpaid : InvoicePaymentStatus.Partial;

                        if (invoice.PaymentStatus != InvoicePaymentStatus.Paid)
                        {
                            var targetComms = commissions.Where(c => c.SalesInvoiceId == invoice.Id);
                            foreach (var comm in targetComms)
                            {
                                if (comm.Status == CommissionStatus.Paid)
                                    return Result<int>.Failure($"Cannot bounce cheque automatically. Sales commission for Invoice {invoice.InvoiceNumber} has already been paid.");

                                comm.Status = CommissionStatus.Unearned;
                            }
                        }
                    }
                }

                cheque.OriginalReceipt.IsVoided = true;

                // ==========================================
                // 4. POST THE REVERSAL GENERAL LEDGER ENTRY
                // ==========================================
                var journalLines = new List<JournalLineRequest>
                {
                    // DEBIT: Reinstate Customer's Accounts Receivable debt
                    new JournalLineRequest { AccountId = cheque.Customer.DefaultReceivableAccountId, Debit = cheque.Amount, Credit = 0, Note = $"Bounced Cheque {cheque.ChequeNumber}" },
                    
                    // CREDIT: Reinstate Accounts Payable (If Endorsed), OR Reduce Bank/Vault
                    new JournalLineRequest { AccountId = creditAccountId, Debit = 0, Credit = cheque.Amount, Note = $"Bounce Reversal ({stateNote})" }
                };

                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = request.BounceDate,
                    Description = $"Dishonored Cheque: {cheque.ChequeNumber} - {request.Reason}",
                    Module = "Treasury",
                    ReferenceNo = $"BNC-{cheque.ChequeNumber}",
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception($"GL Reversal Failed: {jResult.Message}");

                // ==========================================
                // 5. UPDATE CHEQUE STATUS
                // ==========================================
                cheque.Status = ChequeStatus.Bounced;
                cheque.BounceReason = request.Reason;

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
