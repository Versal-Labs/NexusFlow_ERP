using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Configs.Commands
{
    public class UpdateNumberSequenceCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string Prefix { get; set; }
        public int NextNumber { get; set; }
        public string Delimiter { get; set; }
    }

    public class UpdateNumberSequenceHandler : IRequestHandler<UpdateNumberSequenceCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public UpdateNumberSequenceHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(UpdateNumberSequenceCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.NumberSequences.FindAsync(new object[] { request.Id }, cancellationToken);

            if (entity == null)
            {
                return Result<int>.Failure("Sequence ID not found.");
            }

            // --- VALIDATION 1: Basic Sanity ---
            if (request.NextNumber < 1)
            {
                return Result<int>.Failure("Next Number must be greater than 0.");
            }

            // --- VALIDATION 2: ANTI-REWIND SECURITY CHECK (The Fix) ---
            // If the user tries to set the number LOWER than what it is currently, block it.
            // This prevents "Unique Key Violation" crashes in production.
            if (request.NextNumber < entity.NextNumber)
            {
                return Result<int>.Failure($"Invalid Operation: You cannot rewind the sequence to {request.NextNumber}. The current value is {entity.NextNumber}. Reducing this will cause duplicate ID errors.");
            }

            // Update Fields
            entity.Prefix = request.Prefix.ToUpper().Trim();
            entity.NextNumber = request.NextNumber;
            entity.Delimiter = request.Delimiter ?? "-"; // Default protection
            entity.LastUsed = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(entity.Id, "Sequence definition updated.");
        }
    }
}
