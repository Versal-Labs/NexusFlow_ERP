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
    public class GetNumberSequencesQuery : IRequest<Result<List<NumberSequenceDto>>>
    {
    }

    public class GetNumberSequencesHandler : IRequestHandler<GetNumberSequencesQuery, Result<List<NumberSequenceDto>>>
    {
        private readonly IErpDbContext _context;

        public GetNumberSequencesHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<NumberSequenceDto>>> Handle(GetNumberSequencesQuery request, CancellationToken cancellationToken)
        {
            var list = await _context.NumberSequences
                .AsNoTracking()
                .OrderBy(x => x.Module)
                .Select(x => new NumberSequenceDto
                {
                    Id = x.Id,
                    Module = x.Module,
                    Prefix = x.Prefix,
                    NextNumber = x.NextNumber,
                    Delimiter = x.Delimiter,
                    Suffix = x.Suffix ?? ""
                })
                .ToListAsync(cancellationToken);

            return Result<List<NumberSequenceDto>>.Success(list);
        }
    }
}
