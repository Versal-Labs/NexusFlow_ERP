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

        public CreateGrnHandler(INumberSequenceService numSequenceService, IErpDbContext context, IStockService stockService, IJournalService journalService)
        {
            _context = context;
            _stockService = stockService;
            _journalService = journalService;
            _numSequenceService = numSequenceService;
        }

        public async Task<Result<int>> Handle(CreateGrnCommand command, CancellationToken cancellationToken)
        {
            // 1. EXECUTION STRATEGY (Atomic Transaction)
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 2. FETCH PO with Deep Graph
                var po = await _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Items)
                        .ThenInclude(i => i.ProductVariant)
                            .ThenInclude(pv => pv.Product)
                    .FirstOrDefaultAsync(p => p.Id == command.PurchaseOrderId, cancellationToken);

                if (po == null) return Result<int>.Failure("Purchase Order not found.");
                if (po.Status == PurchaseOrderStatus.Closed) return Result<int>.Failure("Purchase Order is already Closed.");

                // Fetch Warehouse for GL Overrides
                var warehouse = await _context.Warehouses
                    .FirstOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken);

                if (warehouse == null) return Result<int>.Failure("Destination Warehouse not found.");

                // 3. GENERATE GRN NUMBER (Concurrency Safe)
                var grnNumber = await _numSequenceService.GenerateNextNumberAsync("GRN", cancellationToken);

                var grn = new GRN
                {
                    GrnNumber = grnNumber,
                    ReceivedDate = command.DateReceived,
                    PurchaseOrderId = command.PurchaseOrderId,
                    WarehouseId = command.WarehouseId,
                    SupplierInvoiceNo = command.SupplierInvoiceNo,
                    CreatedBy = "System" // TODO: Wire to IUserService
                };

                var glGrouping = new Dictionary<int, decimal>();
                decimal grnTotalValue = 0;

                // 4. PROCESS LINES AGAINST PO
                foreach (var incomingItem in command.Items)
                {
                    if (incomingItem.QuantityReceived <= 0) continue;

                    var poLine = po.Items.FirstOrDefault(i => i.ProductVariantId == incomingItem.ProductVariantId);
                    if (poLine == null) throw new Exception($"Item ID {incomingItem.ProductVariantId} does not exist on this Purchase Order.");

                    // Optional: Prevent over-receiving (e.g., max 5% tolerance)
                    decimal totalExpected = poLine.QuantityOrdered * 1.05m;
                    if (poLine.QuantityReceived + incomingItem.QuantityReceived > totalExpected)
                    {
                        throw new Exception($"Cannot receive {incomingItem.QuantityReceived} units. It exceeds the PO tolerance limit.");
                    }

                    decimal lineTotal = incomingItem.QuantityReceived * incomingItem.UnitCost;
                    grnTotalValue += lineTotal;

                    // A. Create GRN Line
                    grn.Items.Add(new GRNItem
                    {
                        ProductVariantId = incomingItem.ProductVariantId,
                        QuantityReceived = incomingItem.QuantityReceived,
                        UnitCost = incomingItem.UnitCost,
                        LineTotal = lineTotal
                    });

                    // B. Update PO Status
                    poLine.QuantityReceived += incomingItem.QuantityReceived;

                    // C. Stock Movement
                    var stockResult = await _stockService.ReceiveStockAsync(
                        incomingItem.ProductVariantId,
                        command.WarehouseId,
                        incomingItem.QuantityReceived,
                        incomingItem.UnitCost,
                        grnNumber
                    );

                    if (!stockResult.Succeeded) throw new Exception($"Stock Error: {stockResult.Message}");

                    // D. Accumulate for GL (Warehouse Override -> Product Inventory -> Product COGS -> Fail)
                    int targetAccountId = warehouse.OverrideInventoryAccountId
                                          ?? poLine.ProductVariant?.Product?.InventoryAccountId
                                          ?? poLine.ProductVariant?.Product?.CogsAccountId
                                          ?? 0;

                    if (targetAccountId == 0)
                        throw new Exception($"Product '{poLine.ProductVariant?.Product?.Name}' is missing an Inventory/Expense Account mapping.");

                    if (!glGrouping.ContainsKey(targetAccountId))
                        glGrouping[targetAccountId] = 0;

                    glGrouping[targetAccountId] += lineTotal;
                }

                if (grnTotalValue == 0) throw new Exception("GRN total value cannot be zero.");

                grn.TotalAmount = grnTotalValue;

                // 5. AUTO-CLOSE PO LOGIC
                // If all lines have received at least 99% of ordered qty, close the PO.
                bool allReceived = po.Items.All(i => i.QuantityReceived >= (i.QuantityOrdered * 0.99m));
                po.Status = allReceived ? PurchaseOrderStatus.Closed : PurchaseOrderStatus.Partial;

                _context.GRNs.Add(grn);
                await _context.SaveChangesAsync(cancellationToken);

                // 6. ENTERPRISE FINANCIAL POSTING (GRN Clearing Account)
                // We do NOT hit AP here. We hit Unbilled Receipts (Clearing). The Supplier Bill hits AP.
                var unbilledConfig = await _context.SystemConfigs
                    .FirstOrDefaultAsync(c => c.Key == "Account.Purchasing.UnbilledReceipts", cancellationToken);

                if (unbilledConfig == null)
                    throw new Exception("Global 'Unbilled Receipts' (GRN Clearing) liability account is not configured in SystemConfigs.");

                int grnClearingAccountId = int.Parse(unbilledConfig.Value);

                var journalLines = new List<JournalLineRequest>
                {
                    // CREDIT Line (Accrued Liability / Unbilled Receipts)
                    new JournalLineRequest
                    {
                        AccountId = grnClearingAccountId,
                        Credit = grnTotalValue,
                        Debit = 0,
                        Note = $"Unbilled Receipt: {po.Supplier.Name} | Ref: {command.SupplierInvoiceNo}"
                    }
                };

                // DEBIT Lines (Assets/Expenses grouped dynamically by Account)
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

                var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = command.DateReceived,
                    Description = $"GRN: {grnNumber} for PO {po.PoNumber}",
                    Module = "Purchasing",
                    ReferenceNo = grnNumber,
                    Lines = journalLines
                });

                if (!journalResult.Succeeded)
                {
                    throw new Exception(journalResult.Message ?? journalResult.Errors?.FirstOrDefault() ?? "Journal Entry Failed");
                }

                // 7. COMMIT TRANSACTION
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(grn.Id, $"GRN {grnNumber} processed successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"GRN Failed: {ex.Message}");
            }
        }
    }
}
