using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.StockTakes.Commands
{
    public class InitiateStockTakeCommand : IRequest<Result<int>>
    {
        public int WarehouseId { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class InitiateStockTakeHandler : IRequestHandler<InitiateStockTakeCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequenceService;

        public InitiateStockTakeHandler(IErpDbContext context, INumberSequenceService sequenceService)
        {
            _context = context;
            _sequenceService = sequenceService;
        }

        public async Task<Result<int>> Handle(InitiateStockTakeCommand request, CancellationToken cancellationToken)
        {
            // 1. ENTERPRISE GUARD: Prevent concurrent stock takes on the same warehouse
            var existingActive = await _context.StockTakes
                .AnyAsync(s => s.WarehouseId == request.WarehouseId
                            && (s.Status == StockTakeStatus.Initiated || s.Status == StockTakeStatus.Counted),
                          cancellationToken);

            if (existingActive)
                return Result<int>.Failure("An active stock take is already in progress for this warehouse. Please approve or reject it first.");

            // 2. FETCH STRICT FIFO SNAPSHOT
            var activeLayers = await _context.StockLayers
                .Where(l => l.WarehouseId == request.WarehouseId && !l.IsExhausted && l.RemainingQty > 0)
                .ToListAsync(cancellationToken);

            // Group by Variant to get the SystemQty and calculate the Weighted Average Cost for variance valuation
            var snapshotItems = activeLayers
                .GroupBy(l => l.ProductVariantId)
                .Select(g => new
                {
                    VariantId = g.Key,
                    SystemQty = g.Sum(l => l.RemainingQty),
                    WeightedAvgCost = g.Sum(l => l.RemainingQty * l.UnitCost) / g.Sum(l => l.RemainingQty)
                })
                .ToList();

            if (!snapshotItems.Any())
                return Result<int>.Failure("There is no active stock in this warehouse to count.");

            // 3. GENERATE DOCUMENT NUMBER
            string stNumber = await _sequenceService.GenerateNextNumberAsync("StockTake", cancellationToken);

            var stockTake = new StockTake
            {
                StockTakeNumber = stNumber,
                Date = DateTime.UtcNow,
                WarehouseId = request.WarehouseId,
                Status = StockTakeStatus.Initiated,
                Notes = request.Notes,
                TotalVarianceValue = 0
            };

            foreach (var item in snapshotItems)
            {
                stockTake.Items.Add(new StockTakeItem
                {
                    ProductVariantId = item.VariantId,
                    SystemQty = item.SystemQty,
                    CountedQty = null, // THE BLIND COUNT: Remains null until floor staff submits
                    VarianceQty = 0,
                    UnitCost = item.WeightedAvgCost,
                    VarianceValue = 0
                });
            }

            _context.StockTakes.Add(stockTake);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(stockTake.Id, $"Stock Take {stNumber} initiated. Ready for physical blind count.");
        }
    }
}
