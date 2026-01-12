using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Services
{
    public class StockService : IStockService
    {
        private readonly IErpDbContext _context;

        public StockService(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result> ReceiveStockAsync(int productVariantId, int warehouseId, decimal qty, decimal unitCost, string referenceDoc)
        {
            // 1. Create a New Layer (Always new for incoming goods to preserve specific batch cost)
            var layer = new StockLayer
            {
                ProductVariantId = productVariantId,
                WarehouseId = warehouseId,
                DateReceived = DateTime.UtcNow,
                InitialQty = qty,
                RemainingQty = qty,
                UnitCost = unitCost,
                BatchNo = referenceDoc // e.g., GRN-1001
            };

            // 2. Log Transaction
            var txn = new StockTransaction
            {
                Date = DateTime.UtcNow,
                ProductVariantId = productVariantId,
                WarehouseId = warehouseId,
                Type = StockTransactionType.PurchaseIn,
                Qty = qty,
                UnitCost = unitCost,
                TotalValue = qty * unitCost,
                ReferenceDocNo = referenceDoc
            };

            _context.StockLayers.Add(layer);
            _context.StockTransactions.Add(txn);
            await _context.SaveChangesAsync(CancellationToken.None);

            return Result.Success();
        }

        public async Task<Result> TransferStockAsync(int productVariantId, int sourceWarehouseId, int targetWarehouseId, decimal qty, string referenceDoc)
        {
            // 1. FIFO Logic: Get Layers from Source, Oldest First
            var layers = await _context.StockLayers
                .Where(x => x.ProductVariantId == productVariantId && x.WarehouseId == sourceWarehouseId && x.RemainingQty > 0)
                .OrderBy(x => x.DateReceived) // <--- CRITICAL: Oldest First
                .ToListAsync();

            decimal qtyNeeded = qty;
            decimal totalValueMoved = 0;

            // Check if we have enough stock
            if (layers.Sum(x => x.RemainingQty) < qty)
                return Result.Failure($"Insufficient stock. Available: {layers.Sum(x => x.RemainingQty)}, Requested: {qty}");

            foreach (var layer in layers)
            {
                if (qtyNeeded <= 0) break;

                // How much can we take from this specific layer?
                decimal qtyToTake = Math.Min(layer.RemainingQty, qtyNeeded);

                // A. Deduct from Source Layer
                layer.RemainingQty -= qtyToTake;

                // B. Calculate Cost for this chunk
                decimal costForChunk = qtyToTake * layer.UnitCost;
                totalValueMoved += costForChunk;

                // C. Move to Target (Create New Layer or Add to Existing?)
                // For strict FIFO tracking at Factory, we typically CREATE a new layer at the Factory 
                // inheriting the ORIGINAL Cost and Date.
                var newLayerAtTarget = new StockLayer
                {
                    ProductVariantId = productVariantId,
                    WarehouseId = targetWarehouseId,
                    DateReceived = layer.DateReceived, // Keep original date for aging!
                    InitialQty = qtyToTake,
                    RemainingQty = qtyToTake,
                    UnitCost = layer.UnitCost, // Keep original cost!
                    BatchNo = layer.BatchNo
                };
                _context.StockLayers.Add(newLayerAtTarget);

                // D. Log Transactions (One Out, One In)
                _context.StockTransactions.Add(new StockTransaction
                {
                    Date = DateTime.UtcNow,
                    ProductVariantId = productVariantId,
                    WarehouseId = sourceWarehouseId,
                    Type = StockTransactionType.TransferOut,
                    Qty = -qtyToTake, // Negative
                    UnitCost = layer.UnitCost,
                    TotalValue = -costForChunk,
                    ReferenceDocNo = referenceDoc
                });

                _context.StockTransactions.Add(new StockTransaction
                {
                    Date = DateTime.UtcNow,
                    ProductVariantId = productVariantId,
                    WarehouseId = targetWarehouseId,
                    Type = StockTransactionType.TransferIn,
                    Qty = qtyToTake, // Positive
                    UnitCost = layer.UnitCost,
                    TotalValue = costForChunk,
                    ReferenceDocNo = referenceDoc
                });

                qtyNeeded -= qtyToTake;
            }

            await _context.SaveChangesAsync(CancellationToken.None);
            return Result.Success($"Successfully transferred {qty} units.");
        }

        public async Task<Result<decimal>> ConsumeStockAsync(int productVariantId, int warehouseId, decimal qty, string referenceDoc)
        {
            // 1. Get Layers (Oldest First)
            var layers = await _context.StockLayers
                .Where(x => x.ProductVariantId == productVariantId && x.WarehouseId == warehouseId && x.RemainingQty > 0)
                .OrderBy(x => x.DateReceived)
                .ToListAsync();

            if (layers.Sum(x => x.RemainingQty) < qty)
            {
                return Result<decimal>.Failure($"Insufficient Raw Materials! Need {qty}, but only have {layers.Sum(x => x.RemainingQty)} in this warehouse.");
            }

            decimal qtyNeeded = qty;
            decimal totalCostConsumed = 0;

            foreach (var layer in layers)
            {
                if (qtyNeeded <= 0) break;

                decimal qtyToTake = Math.Min(layer.RemainingQty, qtyNeeded);

                // A. Deduct Physical Stock
                layer.RemainingQty -= qtyToTake;

                // B. Calculate Cost (FIFO)
                // If this layer cost $10/meter, we consume $10 * qty
                decimal costForChunk = qtyToTake * layer.UnitCost;
                totalCostConsumed += costForChunk;

                // C. Log Transaction (Production Out)
                _context.StockTransactions.Add(new StockTransaction
                {
                    Date = DateTime.UtcNow,
                    ProductVariantId = productVariantId,
                    WarehouseId = warehouseId,
                    Type = StockTransactionType.ProductionOut, // New Enum Type
                    Qty = -qtyToTake,
                    UnitCost = layer.UnitCost,
                    TotalValue = -costForChunk,
                    ReferenceDocNo = referenceDoc
                });

                qtyNeeded -= qtyToTake;
            }

            await _context.SaveChangesAsync(CancellationToken.None);

            // Return the Total Value consumed (e.g., $4500 worth of fabric)
            return Result<decimal>.Success(totalCostConsumed);
        }
    }
}
