using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Configs.Commands
{
    public sealed record RepairNumberSequencesCommand : IRequest<Result<int>>;

    public sealed class RepairNumberSequencesHandler : IRequestHandler<RepairNumberSequencesCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public RepairNumberSequencesHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(RepairNumberSequencesCommand request, CancellationToken cancellationToken)
        {
            var existing = await _context.NumberSequences
                .Select(x => x.Module)
                .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

            var missing = NumberSequenceKeys.Defaults.Where(x => !existing.Contains(x.Key)).ToList();
            foreach (var definition in missing)
            {
                _context.NumberSequences.Add(new NumberSequence
                {
                    Module = definition.Key,
                    Prefix = definition.Value,
                    Delimiter = "-",
                    NextNumber = 1,
                    Suffix = string.Empty
                });
            }

            if (missing.Count > 0)
                await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(missing.Count, missing.Count == 0
                ? "All required number sequences are already configured."
                : $"Created {missing.Count} missing number sequence(s).");
        }
    }
}
