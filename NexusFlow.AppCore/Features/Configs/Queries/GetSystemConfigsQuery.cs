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
    public class GetSystemConfigsQuery : IRequest<Result<List<SystemConfigDto>>>
    {
    }

    // Handler
    public class GetSystemConfigsHandler : IRequestHandler<GetSystemConfigsQuery, Result<List<SystemConfigDto>>>
    {
        private readonly IErpDbContext _context;

        public GetSystemConfigsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<SystemConfigDto>>> Handle(GetSystemConfigsQuery request, CancellationToken cancellationToken)
        {
            var configs = await _context.SystemConfigs
                .AsNoTracking() // Performance: Don't track changes for read-only lists
                .OrderBy(c => c.Key)
                .Select(c => new SystemConfigDto
                {
                    Key = c.Key,
                    Value = c.Value,
                    DataType = c.DataType,
                    Description = c.Description
                })
                .ToListAsync(cancellationToken);

            return Result<List<SystemConfigDto>>.Success(configs);
        }
    }
}
