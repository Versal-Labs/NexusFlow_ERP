using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Queries
{

    public class GetWarehousesQuery : IRequest<Result<List<WarehouseDto>>> { }

    public class GetWarehousesHandler : IRequestHandler<GetWarehousesQuery, Result<List<WarehouseDto>>>
    {
        private readonly IErpDbContext _context;
        public GetWarehousesHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<WarehouseDto>>> Handle(GetWarehousesQuery request, CancellationToken ct)
        {
            var data = await _context.Warehouses.AsNoTracking()
                .Select(w => new WarehouseDto { Id = w.Id, Name = w.Name })
                .ToListAsync(ct);
            return Result<List<WarehouseDto>>.Success(data);
        }
    }
}
