using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.StockTakes.Commands
{
    public class SubmitBlindCountRequest
    {
        public int StockTakeId { get; set; }
        public Dictionary<int, decimal> CountedItems { get; set; } // Key: ProductVariantId, Value: CountedQty
    }

    public class SubmitBlindCountCommand : IRequest<Result<int>>
    {
        public SubmitBlindCountRequest Payload { get; set; }
    }

    public class SubmitBlindCountHandler : IRequestHandler<SubmitBlindCountCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public SubmitBlindCountHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(SubmitBlindCountCommand request, CancellationToken cancellationToken)
        {
            var req = request.Payload;

            var stockTake = await _context.StockTakes
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == req.StockTakeId, cancellationToken);

            if (stockTake == null) return Result<int>.Failure("Stock Take not found.");
            if (stockTake.Status != StockTakeStatus.Initiated) return Result<int>.Failure("Only 'Initiated' stock takes can accept counts.");

            decimal totalVarianceValue = 0;

            foreach (var countData in req.CountedItems)
            {
                int variantId = countData.Key;
                decimal countedQty = countData.Value;

                var item = stockTake.Items.FirstOrDefault(i => i.ProductVariantId == variantId);

                if (item != null)
                {
                    // Existing item counted
                    item.CountedQty = countedQty;
                    item.VarianceQty = countedQty - item.SystemQty;
                    item.VarianceValue = item.VarianceQty * item.UnitCost;
                    totalVarianceValue += item.VarianceValue;
                }
                else
                {
                    // THE FLOOR WORKER FOUND AN ITEM NOT IN THE SYSTEM! (Surplus)
                    // We must fetch the product's last known cost or default to 0 to prevent crashes.
                    var variantCost = await _context.StockLayers
                        .Where(l => l.ProductVariantId == variantId)
                        .OrderByDescending(l => l.Id)
                        .Select(l => l.UnitCost)
                        .FirstOrDefaultAsync(cancellationToken);

                    var newItem = new Domain.Entities.Inventory.StockTakeItem
                    {
                        ProductVariantId = variantId,
                        SystemQty = 0,
                        CountedQty = countedQty,
                        VarianceQty = countedQty, // Entirely surplus
                        UnitCost = variantCost,
                        VarianceValue = countedQty * variantCost
                    };
                    stockTake.Items.Add(newItem);
                    totalVarianceValue += newItem.VarianceValue;
                }
            }

            // Mark any uncounted items as 0 (They went missing entirely!)
            foreach (var uncountedItem in stockTake.Items.Where(i => !i.CountedQty.HasValue))
            {
                uncountedItem.CountedQty = 0;
                uncountedItem.VarianceQty = -uncountedItem.SystemQty;
                uncountedItem.VarianceValue = uncountedItem.VarianceQty * uncountedItem.UnitCost;
                totalVarianceValue += uncountedItem.VarianceValue;
            }

            stockTake.Status = StockTakeStatus.Counted;
            stockTake.TotalVarianceValue = totalVarianceValue;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(stockTake.Id, "Blind count submitted successfully. Awaiting Manager Approval.");
        }
    }
}
