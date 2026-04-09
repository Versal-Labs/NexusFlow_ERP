using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Queries
{
    public class GetVariantStockLevelQuery : IRequest<Result<decimal>>
    {
        public int VariantId { get; set; }
        public int WarehouseId { get; set; }
    }

    public class GetVariantStockLevelHandler : IRequestHandler<GetVariantStockLevelQuery, Result<decimal>>
    {
        private readonly IErpDbContext _context;

        public GetVariantStockLevelHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<decimal>> Handle(GetVariantStockLevelQuery request, CancellationToken cancellationToken)
        {
            // Sum all remaining quantity across active (unexhausted) FIFO layers
            decimal availableStock = await _context.StockLayers
                .Where(sl => sl.ProductVariantId == request.VariantId
                          && sl.WarehouseId == request.WarehouseId
                          && !sl.IsExhausted)
                .SumAsync(sl => sl.RemainingQty, cancellationToken);

            return Result<decimal>.Success(availableStock);
        }
    }
}
