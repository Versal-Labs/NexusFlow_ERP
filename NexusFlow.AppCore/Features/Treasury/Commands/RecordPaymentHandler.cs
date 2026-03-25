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

        public RecordPaymentHandler(IErpDbContext context, IJournalService journalService, INumberSequenceService sequenceService)
        {
            _context = context;
            _journalService = journalService;
            _sequenceService = sequenceService;
        }

        public async Task<Result<int>> Handle(RecordPaymentCommand command, CancellationToken cancellationToken)
        {
            if (command.Amount <= 0) return Result<int>.Failure("Amount must be greater than zero.");
            if (command.Type == PaymentType.CustomerReceipt && command.CustomerId == null) return Result<int>.Failure("Customer ID is required.");

            // Strict Validation: Total Allocations must match the receipt amount
            decimal totalAllocated = command.Allocations.Sum(a => a.Amount);
            if (totalAllocated > command.Amount)
                return Result<int>.Failure("Allocated amount cannot exceed the total receipt amount.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                string payRef = await _sequenceService.GenerateNextNumberAsync("Receipt", cancellationToken);

                var payment = new PaymentTransaction
                {
                    ReferenceNo = payRef,
                    Date = command.Date,
                    Type = command.Type,
                    Method = command.Method,
                    Amount = command.Amount,
                    CustomerId = command.CustomerId,
                    RelatedDocumentNo = command.RelatedDocumentNo,
                    Remarks = command.Remarks
                };

                _context.PaymentTransactions.Add(payment);

                // --- THE ALLOCATION ENGINE ---
                if (command.Type == PaymentType.CustomerReceipt && command.Allocations.Any())
                {
                    var invoiceIds = command.Allocations.Select(a => a.InvoiceId).ToList();

                    // Include CommissionLedgers in the query so we can flip them
                    var invoices = await _context.SalesInvoices
                        .Where(i => invoiceIds.Contains(i.Id))
                        .ToDictionaryAsync(i => i.Id, cancellationToken);

                    // Fetch associated unearned commissions in bulk to avoid N+1 queries
                    var unearnedCommissions = await _context.CommissionLedgers
                        .Where(c => invoiceIds.Contains(c.SalesInvoiceId) && c.Status == CommissionStatus.Unearned)
                        .ToListAsync(cancellationToken);

                    foreach (var alloc in command.Allocations)
                    {
                        if (alloc.Amount <= 0) continue;
                        if (!invoices.TryGetValue(alloc.InvoiceId, out var invoice)) continue;

                        // Create the Allocation Link
                        var allocation = new PaymentAllocation
                        {
                            SalesInvoiceId = invoice.Id,
                            AmountAllocated = alloc.Amount
                        };

                        // EF Core 8 requires adding to the context if the parent isn't tracked the same way, 
                        // but since PaymentTransaction is added, this is fine:
                        payment.Allocations.Add(allocation);

                        // Update the Invoice Status
                        invoice.AmountPaid += alloc.Amount;

                        // ========================================================
                        // THE COMMISSION RELEASE TRIGGER (UPDATED FOR CHEQUES)
                        // ========================================================
                        if (invoice.AmountPaid >= invoice.GrandTotal)
                        {
                            invoice.PaymentStatus = InvoicePaymentStatus.Paid;

                            // Determine if we hold or release based on the payment method
                            // (Assuming your PaymentMethod enum has a 'Cheque' value, adjust if named differently)
                            bool isUnclearedCheque = command.Method == PaymentMethod.Cheque;

                            var invoiceCommissions = unearnedCommissions.Where(c => c.SalesInvoiceId == invoice.Id);
                            foreach (var comm in invoiceCommissions)
                            {
                                comm.Status = isUnclearedCheque ? CommissionStatus.PendingClearance : CommissionStatus.ReadyToPay;
                            }
                        }
                        else
                        {
                            invoice.PaymentStatus = InvoicePaymentStatus.Partial;
                        }
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

                // --- FINANCIAL ROUTING (GL POSTING) ---
                var customer = await _context.Customers.FindAsync(new object[] { command.CustomerId }, cancellationToken);
                if (customer == null || customer.DefaultReceivableAccountId == 0) throw new Exception("Customer AR Account mapping missing.");

                var journalLines = new List<JournalLineRequest>
                {
                    // DEBIT: Bank/Cash
                    new() { AccountId = command.AccountId, Debit = command.Amount, Credit = 0, Note = $"Deposit: {payRef}" },
                    // CREDIT: Accounts Receivable
                    new() { AccountId = customer.DefaultReceivableAccountId, Debit = 0, Credit = command.Amount, Note = $"Receipt from {customer.Name}" }
                };

                var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = command.Date,
                    Description = $"Customer Receipt: {payRef}",
                    Module = "Treasury",
                    ReferenceNo = payRef,
                    Lines = journalLines
                });

                if (!journalResult.Succeeded) throw new Exception($"GL Failed: {journalResult.Message}");

                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(payment.Id, "Receipt and Allocations recorded successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Payment failed: {ex.Message}");
            }
        }
    }
}
