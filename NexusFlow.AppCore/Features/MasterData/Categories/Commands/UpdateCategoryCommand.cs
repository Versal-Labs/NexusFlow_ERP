using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Categories.Commands
{
    public class UpdateCategoryCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public UpdateCategoryHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Categories.FindAsync(new object[] { request.Id }, cancellationToken);
            if (entity == null) return Result<int>.Failure($"Category {request.Id} not found.");

            entity.Name = request.Name;
            entity.Code = request.Code?.ToUpper();
            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(entity.Id);
        }
    }
}
