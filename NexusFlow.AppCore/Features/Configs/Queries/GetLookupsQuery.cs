using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Config;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Configs.Queries
{
    public class GetLookupsQuery : IRequest<Result<List<SystemLookupDto>>>
    {
        public string Type { get; set; } // Optional filter
    }

    public class GetLookupsHandler : IRequestHandler<GetLookupsQuery, Result<List<SystemLookupDto>>>
    {
        private readonly IErpDbContext _context;

        public GetLookupsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<SystemLookupDto>>> Handle(GetLookupsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.SystemLookups.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(request.Type))
            {
                query = query.Where(x => x.Type == request.Type);
            }

            var list = await query
                .OrderBy(x => x.Type)
                .ThenBy(x => x.SortOrder)
                .Select(x => new SystemLookupDto
                {
                    Id = x.Id,
                    Type = x.Type,
                    Code = x.Code,
                    Value = x.Value,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive
                })
                .ToListAsync(cancellationToken);

            return Result<List<SystemLookupDto>>.Success(list);
        }
    }
}
