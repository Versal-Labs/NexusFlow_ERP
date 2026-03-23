using MediatR;
using NexusFlow.AppCore.DTOs.Master;
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
        public CategoryDto Category { get; set; }
        public CreateCategoryCommand(CategoryDto category) { Category = category; }
    }

    public class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public CreateCategoryHandler(IErpDbContext context) { _context = context; }

        public async Task<Result<int>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Category;

            var entity = new Category
            {
                Name = dto.Name,
                Code = dto.Code,
                ParentCategoryId = dto.ParentCategoryId,

                // Assign Posting Groups
                SalesAccountId = dto.SalesAccountId,
                InventoryAccountId = dto.InventoryAccountId,
                CogsAccountId = dto.CogsAccountId
            };

            _context.Categories.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(entity.Id, "Category created successfully.");
        }
    }
}
