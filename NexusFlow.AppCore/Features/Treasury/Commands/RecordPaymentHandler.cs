using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Commands
{
    public class RecordPaymentHandler : IRequestHandler<RecordPaymentCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public RecordPaymentHandler(IErpDbContext context, IJournalService journalService, INumberSequenceService sequenceService, IFinancialAccountResolver accountResolver)
        {
            _context = context; _journalService = journalService; _sequenceService = sequenceService; _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(RecordPaymentCommand command, CancellationToken cancellationToken)
        {
            if (command.ReceiptAmount <= 0) return Result<int>.Failure("Receipt amount must be greater than zero.");
            if (command.CustomerId == null) return Result<int>.Failure("Customer is required for a receipt.");

            // EDGE CASE: Overpayment Guard
            decimal totalAllocated = command.Allocations.Sum(a => a.Amount);
            if (totalAllocated > command.ReceiptAmount)
                return Result<int>.Failure($"You cannot allocate more money ({totalAllocated}) than you received ({command.ReceiptAmount}).");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                string payRef = await _sequenceService.GenerateNextNumberAsync("Receipt", cancellationToken);

                // ==========================================
                // PHASE 2 CHEQUE ROUTING LOGIC
                // ==========================================
                int targetGlAccountId = command.AccountId;

                if (command.Method == PaymentMethod.Cheque)
                {
                    if (string.IsNullOrWhiteSpace(command.ChequeNumber) || !command.BankBranchId.HasValue || !command.ChequeDate.HasValue)
                        return Result<int>.Failure("Cheque Number, Bank, and PDC Date are strictly required for Cheque payments.");

                    // OVERRIDE: Cheques must go to the Vault, not directly to a bank.
                    targetGlAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Asset.UndepositedFunds", cancellationToken);
                }

                var payment = new PaymentTransaction
                {
                    ReferenceNo = payRef,
                    Date = command.Date,
                    Type = command.Type,
                    Method = command.Method,
                    Amount = command.ReceiptAmount,
                    CustomerId = command.CustomerId,
                    Remarks = command.Remarks
                };

                _context.PaymentTransactions.Add(payment);

                // ==========================================
                // THE ALLOCATION ENGINE
                // ==========================================
                if (command.Allocations.Any())
                {
                    var invoiceIds = command.Allocations.Select(a => a.InvoiceId).ToList();
                    var invoices = await _context.SalesInvoices.Where(i => invoiceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
                    var unearnedCommissions = await _context.CommissionLedgers.Where(c => invoiceIds.Contains(c.SalesInvoiceId) && c.Status == CommissionStatus.Unearned).ToListAsync(cancellationToken);

                    foreach (var alloc in command.Allocations)
                    {
                        if (alloc.Amount <= 0 || !invoices.TryGetValue(alloc.InvoiceId, out var invoice)) continue;

                        payment.Allocations.Add(new PaymentAllocation { SalesInvoiceId = invoice.Id, AmountAllocated = alloc.Amount });
                        invoice.AmountPaid += alloc.Amount;

                        if (invoice.AmountPaid >= invoice.GrandTotal)
                        {
                            invoice.PaymentStatus = InvoicePaymentStatus.Paid;
                            bool isUnclearedCheque = command.Method == PaymentMethod.Cheque;
                            foreach (var comm in unearnedCommissions.Where(c => c.SalesInvoiceId == invoice.Id))
                            {
                                comm.Status = isUnclearedCheque ? CommissionStatus.PendingClearance : CommissionStatus.ReadyToPay;
                            }
                        }
                        else { invoice.PaymentStatus = InvoicePaymentStatus.Partial; }
                    }
                }

                await _context.SaveChangesAsync(cancellationToken); // Save to generate PaymentTransaction ID

                // ==========================================
                // PHASE 2: GENERATE THE CHEQUE RECORD
                // ==========================================
                if (command.Method == PaymentMethod.Cheque)
                {
                    var cheque = new ChequeRegister
                    {
                        ChequeNumber = command.ChequeNumber!,
                        BankBranchId = command.BankBranchId!.Value,
                        ChequeDate = command.ChequeDate!.Value,
                        Amount = command.ReceiptAmount,
                        CustomerId = command.CustomerId.Value,
                        OriginalReceiptId = payment.Id,
                        Status = ChequeStatus.InSafe
                    };
                    _context.ChequeRegisters.Add(cheque);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // ==========================================
                // DOUBLE-ENTRY GL POSTING (Handles Overpayments Perfectly)
                // ==========================================
                var customer = await _context.Customers.FindAsync(new object[] { command.CustomerId }, cancellationToken);

                var journalLines = new List<JournalLineRequest>
                {
                    // DEBIT: Target Account (Either Cash, Bank, or Undeposited Funds for Cheques)
                    new() { AccountId = targetGlAccountId, Debit = command.ReceiptAmount, Credit = 0, Note = $"Deposit: {payRef}" },
                    // CREDIT: Full amount to AR (Any unallocated amount naturally creates a customer credit balance)
                    new() { AccountId = customer.DefaultReceivableAccountId, Debit = 0, Credit = command.ReceiptAmount, Note = $"Receipt from {customer.Name}" }
                };

                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = command.Date,
                    Description = $"Customer Receipt: {payRef}",
                    Module = "Treasury",
                    ReferenceNo = payRef,
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception($"GL Failed: {jResult.Message}");

                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(payment.Id, "Receipt recorded successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Payment failed: {ex.Message}");
            }
        }
    }
}
