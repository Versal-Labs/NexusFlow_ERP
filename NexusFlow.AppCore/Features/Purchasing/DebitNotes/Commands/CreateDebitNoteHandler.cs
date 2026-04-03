using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.DebitNotes.Commands
{
    public class CreateDebitNoteRequest
    {
        public int SupplierBillId { get; set; }
        public int DispatchWarehouseId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public Dictionary<int, decimal> ReturnedItems { get; set; } = new(); // VariantId -> Qty
    }

    public class CreateDebitNoteCommand : IRequest<Result<int>>
    {
        public CreateDebitNoteRequest Payload { get; set; } = null!;
    }

    public class CreateDebitNoteHandler : IRequestHandler<CreateDebitNoteCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public CreateDebitNoteHandler(IFinancialAccountResolver accountResolver, IErpDbContext context, IStockService stockService, IJournalService journalService, INumberSequenceService sequenceService)
        {
            _context = context; _stockService = stockService; _journalService = journalService;
            _sequenceService = sequenceService; _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(CreateDebitNoteCommand request, CancellationToken cancellationToken)
        {
            var req = request.Payload;
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                var bill = await _context.SupplierBills
                    .Include(b => b.Supplier)
                    .Include(b => b.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v!.Product).ThenInclude(p => p.Category)
                    .FirstOrDefaultAsync(b => b.Id == req.SupplierBillId, cancellationToken);

                if (bill == null) return Result<int>.Failure("Original Supplier Bill not found.");

                string dnNo = await _sequenceService.GenerateNextNumberAsync("DebitNote", cancellationToken);

                // Note: Assuming you have a DebitNote entity similar to CreditNote
                var debitNote = new SupplierBill // Or DebitNote if you created a dedicated entity
                {
                    BillNumber = dnNo,
                    BillDate = DateTime.UtcNow,
                    SupplierId = bill.SupplierId,
                    Remarks = req.Reason,
                    IsPosted = true,
                    SubTotal = 0,
                    TaxAmount = 0,
                    GrandTotal = 0
                };

                // Trackers for GL Posting
                decimal totalApRefund = 0;
                decimal totalFifoInventoryValueRemoved = 0;
                var inventoryCreditGroup = new Dictionary<int, decimal>();

                foreach (var returnedItem in req.ReturnedItems)
                {
                    if (returnedItem.Value <= 0) continue;
                    var originalLine = bill.Items.FirstOrDefault(i => i.ProductVariantId == returnedItem.Key);
                    if (originalLine == null) throw new Exception("Item not on original bill.");

                    // 1. AP REFUND VALUE (What the supplier owes us back)
                    decimal refundLineTotal = originalLine.UnitPrice * returnedItem.Value;
                    totalApRefund += refundLineTotal;

                    // 2. STRICT FIFO DEDUCTION (What leaves our warehouse)
                    // Assuming IssueStockAsync strictly consumes older layers and returns the exact $$ value consumed.
                    decimal exactFifoValueConsumed = await _stockService.IssueStockAsync(
                        returnedItem.Key, req.DispatchWarehouseId, returnedItem.Value, dnNo, req.Reason);

                    totalFifoInventoryValueRemoved += exactFifoValueConsumed;

                    // Group Inventory Credits by Category GL
                    int invAcc = originalLine.ProductVariant!.Product.Category.InventoryAccountId ?? 0;
                    if (!inventoryCreditGroup.ContainsKey(invAcc)) inventoryCreditGroup[invAcc] = 0;
                    inventoryCreditGroup[invAcc] += exactFifoValueConsumed;

                    // Add Line to Document
                    debitNote.Items.Add(new SupplierBillItem
                    {
                        ProductVariantId = returnedItem.Key,
                        Quantity = returnedItem.Value,
                        UnitPrice = originalLine.UnitPrice,
                        LineTotal = refundLineTotal,
                        Description = $"RMA Return - {originalLine.Description}"
                    });
                }

                debitNote.SubTotal = totalApRefund;
                if (bill.ApplyVat)
                {
                    // Calculate proportional tax to reverse
                    decimal taxRatio = bill.TaxAmount / bill.SubTotal;
                    debitNote.TaxAmount = debitNote.SubTotal * taxRatio;
                }
                debitNote.GrandTotal = debitNote.SubTotal + debitNote.TaxAmount;

                _context.SupplierBills.Add(debitNote); // Or _context.DebitNotes.Add()
                await _context.SaveChangesAsync(cancellationToken);

                // ==========================================
                // DOUBLE-ENTRY GL WITH VARIANCE HANDLING
                // ==========================================
                var journalLines = new List<JournalLineRequest>
                {
                    // DEBIT: Accounts Payable (Reduces our debt to supplier by the exact Invoice amount)
                    new() { AccountId = bill.Supplier.DefaultPayableAccountId!.Value, Debit = debitNote.GrandTotal, Credit = 0, Note = $"Debit Note AP Reversal - {dnNo}" }
                };

                // CREDIT: Inventory Asset (At STRICT FIFO Cost)
                foreach (var inv in inventoryCreditGroup)
                    journalLines.Add(new() { AccountId = inv.Key, Debit = 0, Credit = inv.Value, Note = $"RMA Stock Deduction - {dnNo}" });

                // CREDIT: Revert Input VAT
                if (debitNote.TaxAmount > 0)
                {
                    var taxId = await _accountResolver.ResolveAccountIdAsync("Account.Tax.VATReceivable", cancellationToken);
                    journalLines.Add(new() { AccountId = taxId, Debit = 0, Credit = debitNote.TaxAmount, Note = $"RMA VAT Reversal - {dnNo}" });
                }

                // BALANCING ACT: Purchase Variance
                // If FIFO cost removed ($10.50) is greater than AP Refund ($10.00), we took a $0.50 loss.
                decimal variance = totalFifoInventoryValueRemoved - totalApRefund;
                if (variance != 0)
                {
                    var varianceAccId = await _accountResolver.ResolveAccountIdAsync("Account.Expense.PurchaseVariance", cancellationToken);
                    if (variance > 0) journalLines.Add(new() { AccountId = varianceAccId, Debit = variance, Credit = 0, Note = $"RMA FIFO Loss - {dnNo}" });
                    else journalLines.Add(new() { AccountId = varianceAccId, Debit = 0, Credit = Math.Abs(variance), Note = $"RMA FIFO Gain - {dnNo}" });
                }

                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = debitNote.BillDate,
                    Description = $"Debit Note: {dnNo} (Returns for Bill {bill.BillNumber})",
                    Module = "PurchaseReturns",
                    ReferenceNo = dnNo,
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception($"GL Posting Failed: {jResult.Message}");

                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(debitNote.Id, $"Debit Note {dnNo} posted successfully. Stock deducted via FIFO.");
            }
            catch (Exception ex) { await transaction.RollbackAsync(cancellationToken); return Result<int>.Failure($"RMA Failed: {ex.Message}"); }
        }
    }
}
