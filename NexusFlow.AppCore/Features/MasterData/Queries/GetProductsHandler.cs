using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Queries
{
    // DTO for the list view (lighter than the full entity)
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
    }

    public class GetProductsHandler : IRequestHandler<GetProductsQuery, Result<List<ProductDto>>>
    {
        private readonly IErpDbContext _context;

        public GetProductsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<ProductDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
        {
            var products = await _context.ProductVariants
                .AsNoTracking() // Read-Only optimization
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    SellingPrice = p.SellingPrice
                })
                .ToListAsync(cancellationToken);

            return Result<List<ProductDto>>.Success(products);
        }
    }
}
