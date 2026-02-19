using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Commands
{
    public class CreateGrnHandler : IRequestHandler<CreateGrnCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _numSequenceService;

        public CreateGrnHandler(IErpDbContext context, IStockService stockService, IJournalService journalService)
        {
            _context = context;
            _stockService = stockService;
            _journalService = journalService;
        }

        public async Task<Result<int>> Handle(CreateGrnCommand command, CancellationToken cancellationToken)
        {
            // 1. EXECUTION STRATEGY (Use Transaction for Atomicity)
            // If Stock updates or GL posting fails, the GRN is rolled back.
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 2. FETCH PO with Deep Graph (Product -> InventoryAccount)
                var po = await _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Items)
                        .ThenInclude(i => i.ProductVariant)
                            .ThenInclude(pv => pv.Product) // Needed for InventoryAccountId
                    .FirstOrDefaultAsync(p => p.Id == command.PurchaseOrderId, cancellationToken);

                if (po == null) return Result<int>.Failure("Purchase Order not found.");
                if (po.Status == PurchaseOrderStatus.Closed) return Result<int>.Failure("PO is already Closed.");

                // 3. GENERATE GRN NUMBER (Concurrency Safe)
                // In production, use a Database Sequence or a dedicated Numbering Service.
                // Here is a simpler timestamp-based fallback that avoids Count() collisions.
                var grnNumber = await _numSequenceService.GenerateNextNumberAsync("Purchasing", cancellationToken); ;

                var grn = new GRN
                {
                    GrnNumber = grnNumber,
                    ReceivedDate = command.DateReceived,
                    PurchaseOrderId = command.PurchaseOrderId,
                    WarehouseId = command.WarehouseId,
                    SupplierInvoiceNo = command.SupplierInvoiceNo,
                    CreatedBy = "Admin"
                };

                // Dictionary to group costs by Inventory Account (For Journal Entry)
                // Key: InventoryAccountId, Value: Total Amount
                var glGrouping = new Dictionary<int, decimal>();

                decimal grnTotalValue = 0;

                // 4. PROCESS LINES
                foreach (var incomingItem in command.Items)
                {
                    if (incomingItem.QuantityReceived <= 0) continue;

                    var poLine = po.Items.FirstOrDefault(i => i.ProductVariantId == incomingItem.ProductVariantId);
                    if (poLine == null) continue;

                    // A. Validation: Prevent Over-Receiving (Optional, strictly speaking)
                    if ((poLine.QuantityReceived + incomingItem.QuantityReceived) > poLine.QuantityOrdered)
                    {
                        // You might want to allow this with a warning, or block it. 
                        // For now, we proceed but log it.
                    }

                    decimal lineTotal = incomingItem.QuantityReceived * incomingItem.UnitCost;
                    grnTotalValue += lineTotal;

                    // B. Create GRN Line
                    grn.Items.Add(new GRNItem
                    {
                        ProductVariantId = incomingItem.ProductVariantId,
                        QuantityReceived = incomingItem.QuantityReceived,
                        UnitCost = incomingItem.UnitCost,
                        LineTotal = lineTotal
                    });

                    // C. Update PO Status
                    poLine.QuantityReceived += incomingItem.QuantityReceived;

                    // D. Stock Movement
                    await _stockService.ReceiveStockAsync(
                        incomingItem.ProductVariantId,
                        command.WarehouseId,
                        incomingItem.QuantityReceived,
                        incomingItem.UnitCost,
                        grnNumber
                    );

                    // E. Accumulate for GL (The "Bucket" Fix)
                    // We grab the account from the Product Master (Phase 2 work)
                    // If Product is a Service (InventoryAccount is null), we use an Expense Account fallback.
                    int targetAccountId = poLine.ProductVariant.Product.InventoryAccountId
                                          ?? poLine.ProductVariant.Product.CogsAccountId; // Fallback for services

                    if (!glGrouping.ContainsKey(targetAccountId))
                        glGrouping[targetAccountId] = 0;

                    glGrouping[targetAccountId] += lineTotal;
                }

                grn.TotalAmount = grnTotalValue;

                // 5. AUTO-CLOSE PO LOGIC
                // If all items received within 99% tolerance, close PO
                bool allReceived = po.Items.All(i => i.QuantityReceived >= (i.QuantityOrdered * 0.99m));
                if (allReceived) po.Status = PurchaseOrderStatus.Closed;
                else po.Status = PurchaseOrderStatus.Partial;

                _context.GRNs.Add(grn);
                await _context.SaveChangesAsync(cancellationToken);

                // 6. FINANCIAL POSTING (Grouped)
                // This creates a clean Journal with 1 Credit line and Multiple Debit lines

                // Credit: Accounts Payable
                int apAccountId = po.Supplier.DefaultPayableAccountId ?? 0;
                if (apAccountId == 0) throw new Exception($"Supplier {po.Supplier.Name} has no Payable Account configured.");

                var journalLines = new List<JournalLineRequest>();

                // CREDIT Line (Liability)
                journalLines.Add(new JournalLineRequest
                {
                    AccountId = apAccountId,
                    Credit = grnTotalValue,
                    Debit = 0,
                    Note = $"Supplier: {po.Supplier.Name} | Invoice: {command.SupplierInvoiceNo}"
                });

                // DEBIT Lines (Assets/Expenses grouped by Account)
                foreach (var entry in glGrouping)
                {
                    journalLines.Add(new JournalLineRequest
                    {
                        AccountId = entry.Key,
                        Debit = entry.Value,
                        Credit = 0,
                        Note = $"Stock Receipt - {grnNumber}"
                    });
                }

                await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = command.DateReceived,
                    Description = $"GRN: {grnNumber} for PO {po.PoNumber}",
                    Module = "Purchasing",
                    ReferenceNo = grnNumber,
                    Lines = journalLines
                });

                // 7. COMMIT TRANSACTION
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(grn.Id, "GRN processed successfully.");
            }
            catch (Exception ex)
            {
                // Rollback everything if any step fails
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"GRN Failed: {ex.Message}");
            }
        }
    }
}
