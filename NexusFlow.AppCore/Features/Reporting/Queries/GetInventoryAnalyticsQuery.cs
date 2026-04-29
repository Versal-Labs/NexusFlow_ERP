using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Reporting.Queries
{
    public class InventoryAnalyticsDto
    {
        public string Date { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public string Warehouse { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class GetInventoryAnalyticsQuery : IRequest<Result<List<InventoryAnalyticsDto>>>
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? WarehouseId { get; set; }
        public int? ProductVariantId { get; set; }
        public StockTransactionType? TransactionType { get; set; }
    }

    public class GetInventoryAnalyticsHandler : IRequestHandler<GetInventoryAnalyticsQuery, Result<List<InventoryAnalyticsDto>>>
    {
        private readonly IErpDbContext _context;
        public GetInventoryAnalyticsHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<InventoryAnalyticsDto>>> Handle(GetInventoryAnalyticsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.StockTransactions
                .Include(t => t.Warehouse)
                .Include(t => t.ProductVariant)
                    .ThenInclude(v => v.Product)
                .AsQueryable();

            if (request.StartDate.HasValue) query = query.Where(t => t.Date >= request.StartDate.Value);
            if (request.EndDate.HasValue) query = query.Where(t => t.Date <= request.EndDate.Value.AddDays(1).AddTicks(-1));
            if (request.WarehouseId.HasValue) query = query.Where(t => t.WarehouseId == request.WarehouseId.Value);
            if (request.ProductVariantId.HasValue) query = query.Where(t => t.ProductVariantId == request.ProductVariantId.Value);
            if (request.TransactionType.HasValue) query = query.Where(t => t.Type == request.TransactionType.Value);

            var data = await query
                .OrderByDescending(t => t.Date)
                .Select(t => new InventoryAnalyticsDto
                {
                    Date = t.Date.ToString("yyyy-MM-dd HH:mm"),
                    TransactionType = t.Type.ToString(),
                    ReferenceNo = t.ReferenceDocNo,
                    Warehouse = t.Warehouse.Name,
                    Product = t.ProductVariant.Product.Name,
                    SKU = t.ProductVariant.SKU,
                    Quantity = t.Qty,
                    UnitCost = t.UnitCost,
                    TotalValue = t.TotalValue
                })
                .ToListAsync(cancellationToken);

            return Result<List<InventoryAnalyticsDto>>.Success(data);
        }
    }
}
