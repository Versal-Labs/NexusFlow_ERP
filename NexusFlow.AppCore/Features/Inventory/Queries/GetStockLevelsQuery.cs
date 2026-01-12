using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Queries
{
    public class StockLevelDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string SKU { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal AverageCost { get; set; } // Useful for estimation
        public decimal TotalValue { get; set; }
    }

    public class GetStockLevelsQuery : IRequest<Result<List<StockLevelDto>>>
    {
        public int? WarehouseId { get; set; } // Optional filter
    }

    public class GetStockLevelsHandler : IRequestHandler<GetStockLevelsQuery, Result<List<StockLevelDto>>>
    {
        private readonly IErpDbContext _context;

        public GetStockLevelsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<StockLevelDto>>> Handle(GetStockLevelsQuery request, CancellationToken cancellationToken)
        {
            // 1. Filter: Get only layers that still have stock (RemainingQty > 0)
            var query = _context.StockLayers
                .Include(s => s.ProductVariant)
                .Include(s => s.Warehouse)
                .Where(s => s.RemainingQty > 0);

            // 2. Apply Warehouse Filter if provided
            if (request.WarehouseId.HasValue)
            {
                query = query.Where(s => s.WarehouseId == request.WarehouseId.Value);
            }

            // 3. Aggregate: Group by Product + Warehouse
            // Since we have multiple FIFO layers for the same product, we sum them up.
            var stockData = await query
                .GroupBy(s => new {
                    s.ProductVariantId,
                    s.ProductVariant.Name,
                    s.ProductVariant.SKU,
                    s.WarehouseId,
                    WarehouseName = s.Warehouse.Name
                })
                .Select(g => new StockLevelDto
                {
                    ProductId = g.Key.ProductVariantId,
                    ProductName = g.Key.Name,
                    SKU = g.Key.SKU,
                    WarehouseId = g.Key.WarehouseId,
                    WarehouseName = g.Key.WarehouseName,

                    // Summing the layers
                    QuantityOnHand = g.Sum(x => x.RemainingQty),
                    TotalValue = g.Sum(x => x.RemainingQty * x.UnitCost)
                })
                .ToListAsync(cancellationToken);

            // 4. Calculate Average Cost in memory
            foreach (var item in stockData)
            {
                if (item.QuantityOnHand > 0)
                {
                    // Weighted Average Cost = Total Value / Total Qty
                    item.AverageCost = item.TotalValue / item.QuantityOnHand;
                }
            }

            return Result<List<StockLevelDto>>.Success(stockData);
        }
    }
}
