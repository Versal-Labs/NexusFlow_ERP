using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.CreditNotes.Commands
{
    public class CreateCreditNoteRequest
    {
        public int SalesInvoiceId { get; set; }
        public int ReturnWarehouseId { get; set; }
        public string Reason { get; set; }
        public Dictionary<int, decimal> ReturnedItems { get; set; } // Key: VariantId, Value: Qty Returned
    }

    public class CreateCreditNoteCommand : IRequest<Result<int>>
    {
        public CreateCreditNoteRequest Payload { get; set; }
    }

    public class CreateCreditNoteHandler : IRequestHandler<CreateCreditNoteCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public CreateCreditNoteHandler(IFinancialAccountResolver accountResolver, IErpDbContext context, IStockService stockService, IJournalService journalService, INumberSequenceService sequenceService)
        {
            _context = context;
            _stockService = stockService;
            _journalService = journalService;
            _sequenceService = sequenceService;
            _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(CreateCreditNoteCommand request, CancellationToken cancellationToken)
        {
            var req = request.Payload;
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                var invoice = await _context.SalesInvoices
                    .Include(i => i.Customer)
                    .Include(i => i.Items)
                        .ThenInclude(it => it.ProductVariant)
                            .ThenInclude(v => v.Product)
                                .ThenInclude(p => p.Category)
                    .FirstOrDefaultAsync(i => i.Id == req.SalesInvoiceId, cancellationToken);

                if (invoice == null) return Result<int>.Failure("Original Invoice not found.");

                string cnNo = await _sequenceService.GenerateNextNumberAsync("CreditNote", cancellationToken);

                var creditNote = new CreditNote
                {
                    CreditNoteNumber = cnNo,
                    Date = DateTime.UtcNow,
                    SalesInvoiceId = invoice.Id,
                    CustomerId = invoice.CustomerId,
                    ReturnToWarehouseId = req.ReturnWarehouseId,
                    Reason = req.Reason,
                    IsPosted = true,
                    SubTotal = 0,
                    TotalTax = 0,
                    GrandTotal = 0
                };

                var revenueGroup = new Dictionary<int, decimal>();
                var cogsGroup = new Dictionary<int, decimal>();
                var inventoryAssetGroup = new Dictionary<int, decimal>();

                foreach (var returnedItem in req.ReturnedItems)
                {
                    int variantId = returnedItem.Key;
                    decimal returnQty = returnedItem.Value;

                    if (returnQty <= 0) continue;

                    var originalLine = invoice.Items.FirstOrDefault(i => i.ProductVariantId == variantId);
                    if (originalLine == null) throw new Exception("Returned item was not on the original invoice.");
                    if (returnQty > originalLine.Quantity) throw new Exception($"Cannot return more than originally invoiced for {originalLine.Description}.");

                    decimal originalNetUnitPrice = originalLine.LineTotal / originalLine.Quantity;
                    decimal reversedLineTotal = originalNetUnitPrice * returnQty;

                    var category = originalLine.ProductVariant.Product.Category;
                    decimal estimatedReversedCogs = 0;

                    var cnItem = new CreditNoteItem
                    {
                        ProductVariantId = variantId,
                        ReturnedQuantity = returnQty,
                        UnitPrice = originalNetUnitPrice,
                        LineTotal = reversedLineTotal,
                        RestoredCogsValue = estimatedReversedCogs
                    };

                    creditNote.Items.Add(cnItem);
                    creditNote.SubTotal += reversedLineTotal;

                    int revAcc = category.SalesAccountId ?? 0;
                    if (!revenueGroup.ContainsKey(revAcc)) revenueGroup[revAcc] = 0;
                    revenueGroup[revAcc] += reversedLineTotal;

                    if (originalLine.ProductVariant.Product.Type != ProductType.Service)
                    {
                        await _stockService.RestoreStockAsync(variantId, req.ReturnWarehouseId, returnQty, estimatedReversedCogs, cnNo, req.Reason);

                        int cogsAcc = category.CogsAccountId ?? 0;
                        int invAcc = category.InventoryAccountId ?? 0;
                        if (!cogsGroup.ContainsKey(cogsAcc)) cogsGroup[cogsAcc] = 0;
                        if (!inventoryAssetGroup.ContainsKey(invAcc)) inventoryAssetGroup[invAcc] = 0;

                        cogsGroup[cogsAcc] += estimatedReversedCogs;
                        inventoryAssetGroup[invAcc] += estimatedReversedCogs;
                    }
                }

                if (invoice.TotalTax > 0 && invoice.SubTotal > 0)
                {
                    decimal taxRatio = invoice.TotalTax / invoice.SubTotal;
                    creditNote.TotalTax = creditNote.SubTotal * taxRatio;
                }
                creditNote.GrandTotal = creditNote.SubTotal + creditNote.TotalTax;

                _context.CreditNotes.Add(creditNote);

                // =====================================================================
                // TIER-1 FEATURE: SAFE PROPORTIONAL COMMISSION CLAWBACK
                // =====================================================================
                if (invoice.SalesRepId.HasValue && invoice.SubTotal > 0) // DIVIDE-BY-ZERO GUARD
                {
                    var originalCommission = await _context.CommissionLedgers
                        .Where(c => c.SalesInvoiceId == invoice.Id)
                        .SumAsync(c => c.CommissionAmount, cancellationToken);

                    if (originalCommission > 0)
                    {
                        // Because this is a ratio, it perfectly claws back the mathematically exact amount
                        // regardless of whether the original commission was %-based or Value-based.
                        decimal returnRatio = creditNote.SubTotal / invoice.SubTotal;
                        decimal clawbackAmount = originalCommission * returnRatio;

                        _context.CommissionLedgers.Add(new CommissionLedger
                        {
                            SalesRepId = invoice.SalesRepId.Value,
                            SalesInvoiceId = invoice.Id,
                            CommissionAmount = -clawbackAmount,
                            Status = CommissionStatus.ReadyToPay
                        });
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

                // =====================================================================
                // 5. DOUBLE ENTRY GL REVERSAL
                // =====================================================================
                var journalLines = new List<JournalLineRequest>
                {
                    new JournalLineRequest { AccountId = invoice.Customer.DefaultReceivableAccountId, Debit = 0, Credit = creditNote.GrandTotal, Note = $"RMA Reversal AR - {cnNo}" }
                };

                foreach (var rev in revenueGroup) journalLines.Add(new JournalLineRequest { AccountId = rev.Key, Debit = rev.Value, Credit = 0, Note = $"RMA Reversal Rev - {cnNo}" });

                if (creditNote.TotalTax > 0)
                {
                    var taxId = await _accountResolver.ResolveAccountIdAsync("Account.Tax.VATPayable", cancellationToken);
                    if (taxId == null) throw new InvalidOperationException("Global Tax Payable account is not configured.");
                    journalLines.Add(new JournalLineRequest { AccountId = taxId, Debit = creditNote.TotalTax, Credit = 0, Note = $"RMA Reversal VAT - {cnNo}" });
                }

                foreach (var cogs in cogsGroup) journalLines.Add(new JournalLineRequest { AccountId = cogs.Key, Debit = 0, Credit = cogs.Value, Note = $"RMA Reversal COGS - {cnNo}" });
                foreach (var inv in inventoryAssetGroup) journalLines.Add(new JournalLineRequest { AccountId = inv.Key, Debit = inv.Value, Credit = 0, Note = $"RMA Stock Restore - {cnNo}" });

                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = creditNote.Date,
                    Description = $"Credit Note: {cnNo} (Returns for Inv {invoice.InvoiceNumber})",
                    Module = "SalesReturns",
                    ReferenceNo = cnNo,
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception($"GL Posting Failed: {jResult.Message}");

                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(creditNote.Id, $"Credit Note {cnNo} posted successfully. Inventory restored to Quarantine.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"RMA Failed: {ex.Message}");
            }
        }
    }
}
