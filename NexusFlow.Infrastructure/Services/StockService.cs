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

        public async Task<Result<decimal>> ReceiveStockAsync(int productVariantId, int warehouseId, decimal qty, decimal unitCost, string referenceDoc, string notes = "")
        {
            if (qty <= 0) return Result<decimal>.Failure("Receive quantity must be greater than zero.");

            // 1. Create a New Layer (Always new for incoming goods to preserve specific batch cost)
            var layer = new StockLayer
            {
                ProductVariantId = productVariantId,
                WarehouseId = warehouseId,
                DateReceived = DateTime.UtcNow,
                InitialQty = qty,
                RemainingQty = qty,
                UnitCost = unitCost,
                BatchNo = referenceDoc,
                IsExhausted = false // ARCHITECTURAL UPGRADE: Explicitly marked as active
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
                ReferenceDocNo = referenceDoc,
                Notes = string.IsNullOrWhiteSpace(notes) ? "Stock Receipt" : notes // ARCHITECTURAL UPGRADE: Audit trail
            };

            _context.StockLayers.Add(layer);
            _context.StockTransactions.Add(txn);

            // Note: In some handlers (like GRN), SaveChanges is called by the Handler to group with GL entries.
            // Leaving this here if your interface expects the service to commit.
            await _context.SaveChangesAsync(CancellationToken.None);

            return Result<decimal>.Success(txn.TotalValue);
        }

        public async Task<Result> TransferStockAsync(int productVariantId, int sourceWarehouseId, int targetWarehouseId, decimal qty, string referenceDoc, string notes = "")
        {
            if (qty <= 0) return Result.Failure("Transfer quantity must be greater than zero.");

            // 1. FIFO Logic: Get Layers from Source, Oldest First
            var layers = await _context.StockLayers
                // ARCHITECTURAL UPGRADE: Use !IsExhausted for O(1) boolean index scanning
                .Where(x => x.ProductVariantId == productVariantId && x.WarehouseId == sourceWarehouseId && !x.IsExhausted)
                .OrderBy(x => x.DateReceived)
                .ThenBy(x => x.Id) // Tie-breaker for layers received at the exact same millisecond
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

                // ARCHITECTURAL UPGRADE: Flag layer as exhausted to optimize future queries
                if (layer.RemainingQty == 0)
                {
                    layer.IsExhausted = true;
                }

                // B. Calculate Cost for this chunk
                decimal costForChunk = qtyToTake * layer.UnitCost;
                totalValueMoved += costForChunk;

                // C. Move to Target: Create a new layer at the Factory inheriting ORIGINAL Cost and Date.
                var newLayerAtTarget = new StockLayer
                {
                    ProductVariantId = productVariantId,
                    WarehouseId = targetWarehouseId,
                    DateReceived = layer.DateReceived, // Keep original date for aging!
                    InitialQty = qtyToTake,
                    RemainingQty = qtyToTake,
                    UnitCost = layer.UnitCost,         // Keep original cost!
                    BatchNo = layer.BatchNo,
                    IsExhausted = false
                };
                _context.StockLayers.Add(newLayerAtTarget);

                // D. Log Transactions (One Out, One In)
                _context.StockTransactions.Add(new StockTransaction
                {
                    Date = DateTime.UtcNow,
                    ProductVariantId = productVariantId,
                    WarehouseId = sourceWarehouseId,
                    Type = StockTransactionType.TransferOut,
                    Qty = qtyToTake,
                    UnitCost = layer.UnitCost,
                    TotalValue = costForChunk,
                    ReferenceDocNo = referenceDoc,
                    Notes = string.IsNullOrWhiteSpace(notes) ? $"Transfer to WH-{targetWarehouseId}" : notes
                });

                _context.StockTransactions.Add(new StockTransaction
                {
                    Date = DateTime.UtcNow,
                    ProductVariantId = productVariantId,
                    WarehouseId = targetWarehouseId,
                    Type = StockTransactionType.TransferIn,
                    Qty = qtyToTake,
                    UnitCost = layer.UnitCost,
                    TotalValue = costForChunk,
                    ReferenceDocNo = referenceDoc,
                    Notes = string.IsNullOrWhiteSpace(notes) ? $"Transfer from WH-{sourceWarehouseId}" : notes
                });

                qtyNeeded -= qtyToTake;
            }

            await _context.SaveChangesAsync(CancellationToken.None);
            return Result.Success($"Successfully transferred {qty} units.");
        }

        public async Task<Result<decimal>> ConsumeStockAsync(int productVariantId, int warehouseId, decimal qty, string referenceDoc, string notes = "")
        {
            if (qty <= 0) return Result<decimal>.Failure("Consumption quantity must be greater than zero.");

            // 1. Get Layers (Oldest First)
            var layers = await _context.StockLayers
                // ARCHITECTURAL UPGRADE: Use !IsExhausted for performance
                .Where(x => x.ProductVariantId == productVariantId && x.WarehouseId == warehouseId && !x.IsExhausted)
                .OrderBy(x => x.DateReceived)
                .ThenBy(x => x.Id)
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

                // ARCHITECTURAL UPGRADE: Flag layer as exhausted
                if (layer.RemainingQty == 0)
                {
                    layer.IsExhausted = true;
                }

                // B. Calculate Cost (FIFO)
                decimal costForChunk = qtyToTake * layer.UnitCost;
                totalCostConsumed += costForChunk;

                // C. Log Transaction (Production / Sales Out)
                _context.StockTransactions.Add(new StockTransaction
                {
                    Date = DateTime.UtcNow,
                    ProductVariantId = productVariantId,
                    WarehouseId = warehouseId,
                    Type = StockTransactionType.ProductionOut, // Note: Caller can dictate this via a parameter if needed for SalesOut
                    Qty = qtyToTake,
                    UnitCost = layer.UnitCost,
                    TotalValue = costForChunk,
                    ReferenceDocNo = referenceDoc,
                    Notes = string.IsNullOrWhiteSpace(notes) ? "Stock Consumption" : notes
                });

                qtyNeeded -= qtyToTake;
            }

            await _context.SaveChangesAsync(CancellationToken.None);

            // Return the Total Value consumed (e.g., $4500 worth of fabric)
            return Result<decimal>.Success(totalCostConsumed);
        }
    }
}
