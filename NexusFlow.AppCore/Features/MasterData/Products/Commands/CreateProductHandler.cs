using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Commands
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

            // --- 1. Industry Standard Validation ---
            // A Service cannot have Inventory. A Physical Item MUST have Inventory.
            if (dto.Type == Domain.Enums.ProductType.Service && dto.InventoryAccountId.HasValue)
            {
                // Soft warning or auto-correct: Services don't hold stock value.
                dto.InventoryAccountId = null;
            }
            else if (dto.Type != Domain.Enums.ProductType.Service && !dto.InventoryAccountId.HasValue)
            {
                return Result<int>.Failure("Inventory Asset Account is required for Physical Products.");
            }

            // --- 2. Entity Mapping ---
            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                BrandId = dto.BrandId,
                CategoryId = dto.CategoryId,
                UnitOfMeasureId = dto.UnitOfMeasureId,
                Type = dto.Type,

                // Financials
                SalesAccountId = dto.SalesAccountId,
                CogsAccountId = dto.CogsAccountId,
                InventoryAccountId = dto.InventoryAccountId
            };

            // --- 3. Variants ---
            if (dto.Variants != null)
            {
                foreach (var v in dto.Variants)
                {
                    product.Variants.Add(new ProductVariant
                    {
                        Size = v.Size,
                        Color = v.Color,
                        SKU = v.SKU,
                        CostPrice = v.CostPrice,
                        SellingPrice = v.SellingPrice,
                        ReorderLevel = v.ReorderLevel,
                        Name = $"{dto.Name} ({v.Size}/{v.Color})"
                    });
                }
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(product.Id, "Product created successfully.");
        }
    }
}
