using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Commands
{
    public class CreateProductHandler : IRequestHandler<CreateProductCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public CreateProductHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Product;

            // 1. Create Header
            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                BrandId = dto.BrandId,
                CategoryId = dto.CategoryId,
                UnitOfMeasureId = dto.UnitOfMeasureId,
                Type = dto.Type
            };

            // 2. Add Variants
            foreach (var vDto in dto.Variants)
            {
                var variant = new ProductVariant
                {
                    Size = vDto.Size,
                    Color = vDto.Color,
                    SKU = vDto.SKU,
                    CostPrice = vDto.CostPrice,
                    SellingPrice = vDto.SellingPrice,
                    ReorderLevel = vDto.ReorderLevel,
                    // Generate a Display Name automatically
                    Name = $"{dto.Name} - {vDto.Size} - {vDto.Color}"
                };

                product.Variants.Add(variant);
            }

            // 3. Save (EF Core handles the transaction and Foreign Keys automatically)
            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(product.Id, "Product and Variants created successfully.");
        }
    }
}
