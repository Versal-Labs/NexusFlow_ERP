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
            var list = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryDto { Id = c.Id, Name = c.Name, Code = c.Code })
                .ToListAsync(cancellationToken);
            return Result<List<CategoryDto>>.Success(list);
        }
    }
}
