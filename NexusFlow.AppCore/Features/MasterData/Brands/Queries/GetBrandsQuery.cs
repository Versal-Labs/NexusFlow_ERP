using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Brands.Queries
{
    public class GetBrandsQuery : IRequest<Result<List<BrandDto>>>
    {
    }

    public class GetBrandsHandler : IRequestHandler<GetBrandsQuery, Result<List<BrandDto>>>
    {
        private readonly IErpDbContext _context;

        public GetBrandsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<BrandDto>>> Handle(GetBrandsQuery request, CancellationToken cancellationToken)
        {
            var brands = await _context.Brands
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new BrandDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    Description = b.Description
                })
                .ToListAsync(cancellationToken);

            return Result<List<BrandDto>>.Success(brands);
        }
    }
}
