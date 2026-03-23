using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Commands
{
    public class UpdateProductCommand : IRequest<Result<int>>
    {
        // We reuse the ProductDto, but ensure ID is populated
        public ProductDto Product { get; set; }
    }

    public class DeleteProductCommand : IRequest<Result<bool>>
    {
        public int Id { get; set; }
    }

    public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public UpdateProductHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Product;

            // 1. Fetch Existing Product with Variants
            var product = await _context.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == dto.Id, cancellationToken);

            if (product == null)
            {
                return Result<int>.Failure($"Product with ID {dto.Id} not found.");
            }

            // 2. Fetch Category for validation (Arch Correction)
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == dto.CategoryId, cancellationToken);

            if (category == null) return Result<int>.Failure("The selected Category does not exist.");

            if (dto.Type != Domain.Enums.ProductType.Service)
            {
                if (!category.InventoryAccountId.HasValue) return Result<int>.Failure($"Category '{category.Name}' is missing an Inventory Asset Account.");
                if (!category.CogsAccountId.HasValue) return Result<int>.Failure($"Category '{category.Name}' is missing a COGS Account.");
            }
            if (!category.SalesAccountId.HasValue) return Result<int>.Failure($"Category '{category.Name}' is missing a Sales Revenue Account.");

            // 3. Update Header Fields
            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Type = dto.Type;
            product.BrandId = dto.BrandId;
            product.CategoryId = dto.CategoryId;
            product.UnitOfMeasureId = dto.UnitOfMeasureId;
            product.ImageUrl = dto.ImageUrl;
            // NOTE: Account IDs are completely removed from here.

            // 4. Synchronize Variants
            if (dto.Variants != null)
            {
                foreach (var vDto in dto.Variants)
                {
                    if (vDto.Id > 0)
                    {
                        var existingVariant = product.Variants.FirstOrDefault(v => v.Id == vDto.Id);
                        if (existingVariant != null)
                        {
                            existingVariant.Size = string.IsNullOrWhiteSpace(vDto.Size) ? "N/A" : vDto.Size;
                            existingVariant.Color = string.IsNullOrWhiteSpace(vDto.Color) ? "N/A" : vDto.Color;
                            existingVariant.SKU = vDto.SKU;
                            existingVariant.CostPrice = vDto.CostPrice;
                            existingVariant.SellingPrice = vDto.SellingPrice;
                            existingVariant.ReorderLevel = vDto.ReorderLevel;
                            existingVariant.Name = $"{dto.Name} ({existingVariant.Size}/{existingVariant.Color})";
                        }
                    }
                    else
                    {
                        product.Variants.Add(new ProductVariant
                        {
                            Size = string.IsNullOrWhiteSpace(vDto.Size) ? "N/A" : vDto.Size,
                            Color = string.IsNullOrWhiteSpace(vDto.Color) ? "N/A" : vDto.Color,
                            SKU = vDto.SKU,
                            CostPrice = vDto.CostPrice,
                            MovingAverageCost = vDto.CostPrice,
                            SellingPrice = vDto.SellingPrice,
                            ReorderLevel = vDto.ReorderLevel,
                            Name = $"{dto.Name} ({vDto.Size}/{vDto.Color})",
                            IsActive = true
                        });
                    }
                }

                var incomingIds = dto.Variants.Where(v => v.Id > 0).Select(v => v.Id).ToList();
                var variantsToDelete = product.Variants.Where(v => !incomingIds.Contains(v.Id)).ToList();

                foreach (var variantToDelete in variantsToDelete)
                {
                    _context.ProductVariants.Remove(variantToDelete);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(product.Id, "Product updated successfully.");
        }
    }

    public class DeleteProductHandler : IRequestHandler<DeleteProductCommand, Result<bool>>
    {
        private readonly IErpDbContext _context;

        public DeleteProductHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<bool>> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _context.Products.FindAsync(new object[] { request.Id }, cancellationToken);

            if (product == null)
                return Result<bool>.Failure($"Product {request.Id} not found.");

            // In Enterprise ERPs, we typically check for transaction dependencies here.
            // e.g. If StockTransactions exist, block delete.
            // For now, we proceed with delete. 
            // Note: EF Core Cascade configuration on Product->Variants will auto-delete variants.

            _context.Products.Remove(product);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true, "Product deleted successfully.");
        }
    }
}
