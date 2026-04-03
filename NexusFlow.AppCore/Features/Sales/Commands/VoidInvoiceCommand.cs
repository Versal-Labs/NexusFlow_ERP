using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Commands
{
    public record VoidInvoiceCommand(int InvoiceId) : IRequest<Result<int>>;

    public class VoidInvoiceHandler : IRequestHandler<VoidInvoiceCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly IStockService _stockService;

        public VoidInvoiceHandler(IErpDbContext context, IJournalService journalService, IStockService stockService)
        {
            _context = context;
            _journalService = journalService;
            _stockService = stockService; // Correctly using your core service
        }

        public async Task<Result<int>> Handle(VoidInvoiceCommand request, CancellationToken cancellationToken)
        {
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                var invoice = await _context.SalesInvoices
                    .Include(i => i.Items)
                    .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

                if (invoice == null) return Result<int>.Failure("Invoice not found.");

                // 1. ENTERPRISE GUARD: Check if payments exist
                if (invoice.AmountPaid > 0)
                    return Result<int>.Failure("Cannot void an invoice that has payments applied. Please issue a Credit Note (RMA) instead.");

                if (invoice.PaymentStatus == InvoicePaymentStatus.Voided)
                    return Result<int>.Failure("Invoice is already voided.");

                // 2. Void the Invoice Record
                invoice.PaymentStatus = InvoicePaymentStatus.Voided;

                // 3. REVERSE GENERAL LEDGER (Journal Entries)
                var originalJournal = await _context.JournalEntries
                    .Include(j => j.Lines)
                    .FirstOrDefaultAsync(j => j.ReferenceNo == invoice.InvoiceNumber && j.Module == "Sales", cancellationToken);

                if (originalJournal != null)
                {
                    // Create a reversing entry by perfectly swapping Debits and Credits
                    var reverseLines = originalJournal.Lines.Select(l => new JournalLineRequest
                    {
                        AccountId = l.AccountId,
                        Debit = l.Credit,  // Swapped
                        Credit = l.Debit,  // Swapped
                        Note = $"Void Reversal - {invoice.InvoiceNumber}"
                    }).ToList();

                    var journalReq = new JournalEntryRequest
                    {
                        Date = DateTime.UtcNow,
                        Description = $"Void Reversal for Invoice {invoice.InvoiceNumber}",
                        Module = "Sales",
                        ReferenceNo = invoice.InvoiceNumber + "-VOID",
                        Lines = reverseLines
                    };

                    var jResult = await _journalService.PostJournalAsync(journalReq);
                    if (!jResult.Succeeded) throw new Exception($"Failed to post reversing Journal Entry: {jResult.Message}");
                }

                // 4. REVERSE INVENTORY (Put Finished Goods back via IStockService)
                // We query the original transactions to know exactly which warehouse to return to
                var originalStockTransactions = await _context.StockTransactions
                    .Where(st => st.ReferenceDocNo == invoice.InvoiceNumber)
                    .ToListAsync(cancellationToken);

                foreach (var st in originalStockTransactions)
                {
                    // Delegate to your established Stock Service instead of manual EF Core inserts
                    var stockResult = await _stockService.ReceiveStockAsync(
                        st.ProductVariantId,
                        st.WarehouseId,
                        st.Qty,
                        st.UnitCost, // Restoring at the exact original COGS
                        invoice.InvoiceNumber + "-VOID"
                    );

                    if (!stockResult.Succeeded)
                        throw new Exception($"Failed to restore stock for Variant {st.ProductVariantId}: {stockResult.Message}");
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(invoice.Id, $"Invoice {invoice.InvoiceNumber} successfully voided. Stock and Ledger reversed.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Void Operation Failed: {ex.Message}");
            }
        }
    }
}
