using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Queries
{
    public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
    {
        private readonly IErpDbContext _context;

        public GetProductByIdHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.UnitOfMeasure)
                // Include Variants for Accordion/Edit Grid
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (product == null)
            {
                return Result<ProductDto>.Failure($"Product with ID {request.Id} not found.");
            }

            // Map Entity -> DTO
            var dto = new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Type = product.Type,

                // FK IDs (For Select2 Pre-selection)
                BrandId = product.BrandId,
                CategoryId = product.CategoryId,
                UnitOfMeasureId = product.UnitOfMeasureId,


                // Display Names
                BrandName = product.Brand.Name,
                CategoryName = product.Category.Name,
                UnitName = product.UnitOfMeasure.Symbol,

                // Variant List
                Variants = product.Variants.Select(v => new ProductVariantDto
                {
                    Id = v.Id,
                    Size = v.Size,
                    Color = v.Color,
                    SKU = v.SKU,
                    CostPrice = v.CostPrice,
                    SellingPrice = v.SellingPrice,
                    ReorderLevel = v.ReorderLevel
                }).ToList()
            };

            return Result<ProductDto>.Success(dto);
        }
    }
}
