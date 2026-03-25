using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Queries
{
    public class GetAvailableStockQuery : IRequest<Result<decimal>>
    {
        public int ProductVariantId { get; set; }
        public int WarehouseId { get; set; }
    }

    public class GetAvailableStockHandler : IRequestHandler<GetAvailableStockQuery, Result<decimal>>
    {
        private readonly IErpDbContext _context;

        public GetAvailableStockHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<decimal>> Handle(GetAvailableStockQuery request, CancellationToken cancellationToken)
        {
            var availableQty = await _context.StockLayers
                .Where(x => x.ProductVariantId == request.ProductVariantId
                         && x.WarehouseId == request.WarehouseId
                         && !x.IsExhausted)
                .SumAsync(x => x.RemainingQty, cancellationToken);

            return Result<decimal>.Success(availableQty);
        }
    }
}
