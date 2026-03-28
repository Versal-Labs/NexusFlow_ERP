using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.StockTakes.Queries
{
    public class StockTakeDetailsDto
    {
        public int Id { get; set; }
        public string StockTakeNumber { get; set; }
        public int Status { get; set; }
        public List<StockTakeItemDto> Items { get; set; } = new();
    }

    public class StockTakeItemDto
    {
        public int ProductVariantId { get; set; }
        public string Description { get; set; }
        public decimal SystemQty { get; set; }
        public decimal? CountedQty { get; set; }
        public decimal VarianceQty { get; set; }
        public decimal UnitCost { get; set; }
        public decimal VarianceValue { get; set; }
    }

    public class GetStockTakeByIdQuery : IRequest<Result<StockTakeDetailsDto>> { public int Id { get; set; } }

    public class GetStockTakeByIdHandler : IRequestHandler<GetStockTakeByIdQuery, Result<StockTakeDetailsDto>>
    {
        private readonly IErpDbContext _context;
        public GetStockTakeByIdHandler(IErpDbContext context) => _context = context;

        public async Task<Result<StockTakeDetailsDto>> Handle(GetStockTakeByIdQuery request, CancellationToken cancellationToken)
        {
            var stockTake = await _context.StockTakes
                .Include(s => s.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (stockTake == null) return Result<StockTakeDetailsDto>.Failure("Not found.");

            var dto = new StockTakeDetailsDto
            {
                Id = stockTake.Id,
                StockTakeNumber = stockTake.StockTakeNumber,
                Status = (int)stockTake.Status,
                Items = stockTake.Items.Select(i => new StockTakeItemDto
                {
                    ProductVariantId = i.ProductVariantId,
                    Description = $"[{i.ProductVariant.SKU}] {i.ProductVariant.Product.Name}",
                    SystemQty = i.SystemQty,
                    CountedQty = i.CountedQty,
                    VarianceQty = i.VarianceQty,
                    UnitCost = i.UnitCost,
                    VarianceValue = i.VarianceValue
                }).ToList()
            };

            return Result<StockTakeDetailsDto>.Success(dto);
        }
    }
}
