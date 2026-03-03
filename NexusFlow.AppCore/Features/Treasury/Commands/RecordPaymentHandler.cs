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
                    var invoices = await _context.SalesInvoices
                        .Where(i => invoiceIds.Contains(i.Id))
                        .ToDictionaryAsync(i => i.Id, cancellationToken);

                    foreach (var alloc in command.Allocations)
                    {
                        if (alloc.Amount <= 0) continue;
                        if (!invoices.TryGetValue(alloc.InvoiceId, out var invoice)) continue;

                        // Create the Allocation Link
                        payment.Allocations.Add(new PaymentAllocation
                        {
                            SalesInvoiceId = invoice.Id,
                            AmountAllocated = alloc.Amount
                        });

                        // Update the Invoice Status
                        invoice.AmountPaid += alloc.Amount;

                        if (invoice.AmountPaid >= invoice.GrandTotal)
                            invoice.PaymentStatus = InvoicePaymentStatus.Paid;
                        else
                            invoice.PaymentStatus = InvoicePaymentStatus.Partial;
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
