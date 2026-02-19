using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Configs.Commands
{
    public class UpdateLookupCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string Value { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateLookupHandler : IRequestHandler<UpdateLookupCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public UpdateLookupHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(UpdateLookupCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.SystemLookups.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null) return Result<int>.Failure("Lookup not found.");

            entity.Value = request.Value;
            entity.SortOrder = request.SortOrder;
            entity.IsActive = request.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(entity.Id);
        }
    }
}
