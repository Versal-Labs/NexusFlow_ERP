using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Categories.Commands
{
    public class CreateCategoryCommand : IRequest<Result<int>>
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public CreateCategoryHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            var entity = new Category { Name = request.Name, Code = request.Code?.ToUpper() };
            _context.Categories.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(entity.Id);
        }
    }
}
