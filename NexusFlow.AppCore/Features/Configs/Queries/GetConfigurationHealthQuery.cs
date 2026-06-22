using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Configs.Queries
{
    public sealed record GetConfigurationHealthQuery : IRequest<Result<ConfigurationHealthDto>>;

    public sealed class ConfigurationHealthDto
    {
        public List<string> MissingNumberSequences { get; init; } = new();
        public bool IsHealthy => MissingNumberSequences.Count == 0;
    }

    public sealed class GetConfigurationHealthHandler
        : IRequestHandler<GetConfigurationHealthQuery, Result<ConfigurationHealthDto>>
    {
        private readonly IErpDbContext _context;

        public GetConfigurationHealthHandler(IErpDbContext context) => _context = context;

        public async Task<Result<ConfigurationHealthDto>> Handle(GetConfigurationHealthQuery request, CancellationToken cancellationToken)
        {
            var existing = await _context.NumberSequences.AsNoTracking()
                .Select(x => x.Module)
                .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

            return Result<ConfigurationHealthDto>.Success(new ConfigurationHealthDto
            {
                MissingNumberSequences = NumberSequenceKeys.Required.Where(x => !existing.Contains(x)).OrderBy(x => x).ToList()
            });
        }
    }
}
