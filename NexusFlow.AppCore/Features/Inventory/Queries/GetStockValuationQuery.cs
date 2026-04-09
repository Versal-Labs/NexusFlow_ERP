using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Queries
{
    // ==========================================
    // 1. THE DTO
    // ==========================================
    public class StockValuationDto
    {
        public string WarehouseName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalQuantity { get; set; }

        // This is the true mathematical FIFO average of remaining layers
        public decimal BlendedUnitCost { get; set; }
        public decimal TotalFifoValue { get; set; }
    }

    // ==========================================
    // 2. THE QUERY
    // ==========================================
    public class GetStockValuationQuery : IRequest<Result<List<StockValuationDto>>>
    {
        public int? WarehouseId { get; set; }
        public int? CategoryId { get; set; }
        public string? SearchTerm { get; set; }
    }

    // ==========================================
    // 3. THE HANDLER
    // ==========================================
    public class GetStockValuationHandler : IRequestHandler<GetStockValuationQuery, Result<List<StockValuationDto>>>
    {
        private readonly IErpDbContext _context;
        public GetStockValuationHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<StockValuationDto>>> Handle(GetStockValuationQuery request, CancellationToken cancellationToken)
        {
            // 1. Query only ACTIVE (unexhausted) inventory layers
            var query = _context.StockLayers
                .Include(sl => sl.ProductVariant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Category)
                .Include(sl => sl.Warehouse)
                .Where(sl => !sl.IsExhausted && sl.RemainingQty > 0)
                .AsNoTracking()
                .AsQueryable();

            // 2. Apply Filters
            if (request.WarehouseId.HasValue)
                query = query.Where(sl => sl.WarehouseId == request.WarehouseId.Value);

            if (request.CategoryId.HasValue)
                query = query.Where(sl => sl.ProductVariant.Product.CategoryId == request.CategoryId.Value);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(sl => sl.ProductVariant.SKU.ToLower().Contains(term) ||
                                          sl.ProductVariant.Name.ToLower().Contains(term));
            }

            // 3. Execute and Group Data in Memory (Safest for complex math)
            var activeLayers = await query.ToListAsync(cancellationToken);

            var valuationReport = activeLayers
                .GroupBy(sl => new
                {
                    Warehouse = sl.Warehouse.Name,
                    Category = sl.ProductVariant.Product.Category.Name,
                    SKU = sl.ProductVariant.SKU,
                    Product = sl.ProductVariant.Name
                })
                .Select(g =>
                {
                    decimal totalQty = g.Sum(l => l.RemainingQty);
                    decimal totalValue = g.Sum(l => l.RemainingQty * l.UnitCost);

                    return new StockValuationDto
                    {
                        WarehouseName = g.Key.Warehouse,
                        CategoryName = g.Key.Category,
                        Sku = g.Key.SKU,
                        ProductName = g.Key.Product,
                        TotalQuantity = totalQty,
                        TotalFifoValue = totalValue,
                        // Calculate blended cost to prevent divide-by-zero
                        BlendedUnitCost = totalQty > 0 ? (totalValue / totalQty) : 0
                    };
                })
                .OrderBy(r => r.WarehouseName)
                .ThenBy(r => r.CategoryName)
                .ThenBy(r => r.ProductName)
                .ToList();

            return Result<List<StockValuationDto>>.Success(valuationReport);
        }
    }
}
