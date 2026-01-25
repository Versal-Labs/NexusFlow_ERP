using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Units.Commands
{
    public class CreateUnitCommand : IRequest<Result<int>>
    {
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
    }

    public class CreateUnitHandler : IRequestHandler<CreateUnitCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public CreateUnitHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
        {
            var entity = new UnitOfMeasure { Name = request.Name, Symbol = request.Symbol };
            _context.UnitOfMeasures.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(entity.Id);
        }
    }
}
