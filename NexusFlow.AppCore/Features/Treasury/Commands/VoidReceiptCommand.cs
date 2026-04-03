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
    public record VoidReceiptCommand(int ReceiptId) : IRequest<Result<int>>;

    public class VoidReceiptHandler : IRequestHandler<VoidReceiptCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;

        public VoidReceiptHandler(IErpDbContext context, IJournalService journalService)
        {
            _context = context;
            _journalService = journalService;
        }

        public async Task<Result<int>> Handle(VoidReceiptCommand request, CancellationToken cancellationToken)
        {
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. FETCH THE RECEIPT GRAPH (Notice: No .Include(p => p.Customer) here!)
                var receipt = await _context.PaymentTransactions
                    .Include(p => p.Allocations)
                        .ThenInclude(a => a.SalesInvoice)
                    .FirstOrDefaultAsync(p => p.Id == request.ReceiptId, cancellationToken);

                if (receipt == null) return Result<int>.Failure("Receipt not found.");
                if (receipt.IsVoided) return Result<int>.Failure("This receipt is already voided.");

                // 2. ENTERPRISE CHEQUE VAULT GUARD
                if (receipt.Method == PaymentMethod.Cheque)
                {
                    var cheque = await _context.ChequeRegisters.FirstOrDefaultAsync(c => c.OriginalReceiptId == receipt.Id, cancellationToken);
                    if (cheque != null)
                    {
                        if (cheque.Status != ChequeStatus.InSafe)
                        {
                            return Result<int>.Failure("Cannot void receipt: The associated cheque has already been deposited or cleared. Please use the Cheque Vault to 'Bounce' the cheque instead.");
                        }

                        // Cheque is still in safe. We can safely bounce/void it.
                        cheque.Status = ChequeStatus.Bounced;
                        cheque.BounceReason = "Original Receipt Voided";
                    }
                }

                // 3. REINSTATE INVOICES & REVERT COMMISSIONS
                var allocatedInvoiceIds = receipt.Allocations.Select(a => a.SalesInvoiceId).ToList();

                var commissions = await _context.CommissionLedgers
                    .Where(c => allocatedInvoiceIds.Contains(c.SalesInvoiceId))
                    .ToListAsync(cancellationToken);

                foreach (var allocation in receipt.Allocations)
                {
                    var invoice = allocation.SalesInvoice;

                    // Reverse the payment applied
                    invoice.AmountPaid -= allocation.AmountAllocated;

                    // Re-evaluate Invoice Status
                    if (invoice.AmountPaid <= 0)
                        invoice.PaymentStatus = InvoicePaymentStatus.Unpaid;
                    else
                        invoice.PaymentStatus = InvoicePaymentStatus.Partial;

                    // Revert Commission Status back to Unearned
                    var invoiceCommissions = commissions.Where(c => c.SalesInvoiceId == invoice.Id);
                    foreach (var comm in invoiceCommissions)
                    {
                        if (comm.Status == CommissionStatus.Paid)
                            return Result<int>.Failure($"Cannot void receipt: Sales Commission for Invoice {invoice.InvoiceNumber} has already been paid out by Payroll.");

                        comm.Status = CommissionStatus.Unearned;
                    }
                }

                // 4. REVERSE GENERAL LEDGER (Double-Entry Reversal)
                var originalJournal = await _context.JournalEntries
                    .Include(j => j.Lines)
                    .FirstOrDefaultAsync(j => j.ReferenceNo == receipt.ReferenceNo && j.Module == "Treasury", cancellationToken);

                if (originalJournal != null)
                {
                    // Create reversing lines by perfectly swapping Debits and Credits from the original lines
                    var reverseLines = originalJournal.Lines.Select(l => new JournalLineRequest
                    {
                        AccountId = l.AccountId,
                        Debit = l.Credit,  // Swapped
                        Credit = l.Debit,  // Swapped
                        Note = $"Void Reversal - {receipt.ReferenceNo}"
                    }).ToList();

                    var journalReq = new JournalEntryRequest
                    {
                        Date = DateTime.UtcNow,
                        Description = $"Void Reversal for Receipt {receipt.ReferenceNo}",
                        Module = "Treasury",
                        ReferenceNo = receipt.ReferenceNo + "-VOID",
                        Lines = reverseLines
                    };

                    var jResult = await _journalService.PostJournalAsync(journalReq);
                    if (!jResult.Succeeded) throw new Exception($"Failed to post reversing Journal Entry: {jResult.Message}");
                }

                // 5. FINALIZE
                receipt.IsVoided = true;

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(receipt.Id, $"Receipt {receipt.ReferenceNo} successfully voided. AR reinstated and GL reversed.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Void Operation Failed: {ex.Message}");
            }
        }
    }
}
