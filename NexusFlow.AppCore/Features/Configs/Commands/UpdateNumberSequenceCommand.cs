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

            // Validation: Don't allow resetting number to 0 or negative
            if (request.NextNumber < 1)
            {
                return Result<int>.Failure("Next Number must be greater than 0.");
            }

            // Update Fields
            entity.Prefix = request.Prefix.ToUpper().Trim(); // Standardize formatting
            entity.NextNumber = request.NextNumber;
            entity.Delimiter = request.Delimiter;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(entity.Id, "Sequence definition updated.");
        }
    }
}
