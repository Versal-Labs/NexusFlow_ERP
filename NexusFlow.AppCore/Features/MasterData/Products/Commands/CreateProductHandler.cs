using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
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
            var dto = request.Product; // Note: Ensure Account IDs are removed from this DTO class!

            // --- 1. FETCH CATEGORY (To validate Enterprise Posting Groups) ---
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == dto.CategoryId, cancellationToken);

            if (category == null)
            {
                return Result<int>.Failure("The selected Category does not exist.");
            }

            // --- 2. INDUSTRY STANDARD VALIDATION (Financial Hooks) ---
            // A Physical Item MUST have an Inventory Asset and COGS account configured on its Category.
            if (dto.Type != ProductType.Service)
            {
                if (!category.InventoryAccountId.HasValue)
                    return Result<int>.Failure($"Category '{category.Name}' is missing an Inventory Asset Account. Physical products cannot be created here.");

                if (!category.CogsAccountId.HasValue)
                    return Result<int>.Failure($"Category '{category.Name}' is missing a Cost of Goods Sold (COGS) Account.");
            }

            // ALL items (Physical or Service) require a Sales Revenue Account
            if (!category.SalesAccountId.HasValue)
            {
                return Result<int>.Failure($"Category '{category.Name}' is missing a Sales Revenue Account.");
            }

            // --- 3. ENTITY MAPPING ---
            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                BrandId = dto.BrandId,
                CategoryId = dto.CategoryId,
                UnitOfMeasureId = dto.UnitOfMeasureId,
                ImageUrl = dto.ImageUrl,
                Type = dto.Type
                // Notice: No GL Accounts mapped here! They belong to the Category.
            };

            // --- 4. VARIANTS (Including the new Dimensional Upgrades) ---
            if (dto.Variants != null && dto.Variants.Any())
            {
                // Ensure no duplicate SKUs within the incoming request
                var incomingSkus = dto.Variants.Select(v => v.SKU).ToList();
                var existingSkus = await _context.ProductVariants
                    .Where(pv => incomingSkus.Contains(pv.SKU))
                    .Select(pv => pv.SKU)
                    .ToListAsync(cancellationToken);

                if (existingSkus.Any())
                {
                    return Result<int>.Failure($"The following SKUs already exist in the system: {string.Join(", ", existingSkus)}");
                }

                foreach (var v in dto.Variants)
                {
                    product.Variants.Add(new ProductVariant
                    {
                        Size = string.IsNullOrWhiteSpace(v.Size) ? "N/A" : v.Size,
                        Color = string.IsNullOrWhiteSpace(v.Color) ? "N/A" : v.Color,
                        SKU = v.SKU,
                        // Barcode = v.Barcode, // Uncomment if your DTO has this from our earlier upgrade
                        CostPrice = v.CostPrice,
                        MovingAverageCost = v.CostPrice, // Seed initial Moving Average Cost
                        SellingPrice = v.SellingPrice,
                        // MinimumSellingPrice = v.MinimumSellingPrice, // Uncomment if in DTO
                        ReorderLevel = v.ReorderLevel,
                        Name = $"{dto.Name} ({v.Size}/{v.Color})",
                        IsActive = true
                    });
                }
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(product.Id, "Product created successfully.");
        }
    }
}
