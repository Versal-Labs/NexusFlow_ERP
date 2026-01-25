using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Units.Commands
{
    public class UpdateUnitCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
    }

    public class UpdateUnitHandler : IRequestHandler<UpdateUnitCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public UpdateUnitHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.UnitOfMeasures.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null) return Result<int>.Failure($"Unit {request.Id} not found.");

            entity.Name = request.Name;
            entity.Symbol = request.Symbol;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(entity.Id);
        }
    }
}
