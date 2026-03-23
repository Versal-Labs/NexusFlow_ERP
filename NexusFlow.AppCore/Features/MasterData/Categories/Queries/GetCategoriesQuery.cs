using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Categories.Queries
{
    public class GetCategoriesQuery : IRequest<Result<List<CategoryDto>>> { }

    public class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, Result<List<CategoryDto>>>
    {
        private readonly IErpDbContext _context;
        public GetCategoriesHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Include(c => c.ParentCategory)
                .Include(c => c.SalesAccount)
                .Include(c => c.InventoryAccount)
                .Include(c => c.CogsAccount)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Code = c.Code,
                    ParentCategoryId = c.ParentCategoryId,
                    ParentCategoryName = c.ParentCategory != null ? c.ParentCategory.Name : null,

                    SalesAccountId = c.SalesAccountId,
                    SalesAccountName = c.SalesAccount != null ? c.SalesAccount.Name : null,

                    InventoryAccountId = c.InventoryAccountId,
                    InventoryAccountName = c.InventoryAccount != null ? c.InventoryAccount.Name : null,

                    CogsAccountId = c.CogsAccountId,
                    CogsAccountName = c.CogsAccount != null ? c.CogsAccount.Name : null
                })
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            return Result<List<CategoryDto>>.Success(categories);
        }
    }
}
