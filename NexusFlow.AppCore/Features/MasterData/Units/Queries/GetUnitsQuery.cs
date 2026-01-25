using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Units.Queries
{
    public class GetUnitsQuery : IRequest<Result<List<UnitOfMeasureDto>>> { }

    public class GetUnitsHandler : IRequestHandler<GetUnitsQuery, Result<List<UnitOfMeasureDto>>>
    {
        private readonly IErpDbContext _context;
        public GetUnitsHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<UnitOfMeasureDto>>> Handle(GetUnitsQuery request, CancellationToken cancellationToken)
        {
            var list = await _context.UnitOfMeasures
                .AsNoTracking()
                .OrderBy(u => u.Name)
                .Select(u => new UnitOfMeasureDto { Id = u.Id, Name = u.Name, Symbol = u.Symbol })
                .ToListAsync(cancellationToken);
            return Result<List<UnitOfMeasureDto>>.Success(list);
        }
    }
}
