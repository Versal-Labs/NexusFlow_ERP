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

            // 2. Update Header Fields
            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Type = dto.Type;

            // Classification
            product.BrandId = dto.BrandId;
            product.CategoryId = dto.CategoryId;
            product.UnitOfMeasureId = dto.UnitOfMeasureId;

            // Financials (Critical for Accounting Integrity)
            product.SalesAccountId = dto.SalesAccountId;
            product.CogsAccountId = dto.CogsAccountId;
            product.InventoryAccountId = dto.InventoryAccountId;

            // 3. Synchronize Variants (The "Graph Diff" Logic)

            // A. Update Existing & Add New
            if (dto.Variants != null)
            {
                foreach (var vDto in dto.Variants)
                {
                    if (vDto.Id > 0)
                    {
                        // Update Existing Variant
                        var existingVariant = product.Variants.FirstOrDefault(v => v.Id == vDto.Id);
                        if (existingVariant != null)
                        {
                            existingVariant.Size = vDto.Size;
                            existingVariant.Color = vDto.Color;
                            existingVariant.SKU = vDto.SKU;
                            existingVariant.CostPrice = vDto.CostPrice;
                            existingVariant.SellingPrice = vDto.SellingPrice;
                            existingVariant.ReorderLevel = vDto.ReorderLevel;
                            existingVariant.Name = $"{dto.Name} ({vDto.Size}/{vDto.Color})";
                        }
                    }
                    else
                    {
                        // Add New Variant
                        product.Variants.Add(new ProductVariant
                        {
                            Size = vDto.Size,
                            Color = vDto.Color,
                            SKU = vDto.SKU,
                            CostPrice = vDto.CostPrice,
                            SellingPrice = vDto.SellingPrice,
                            ReorderLevel = vDto.ReorderLevel,
                            Name = $"{dto.Name} ({vDto.Size}/{vDto.Color})"
                        });
                    }
                }

                // B. Handle Deletions (Variants in DB but NOT in DTO)
                // We map IDs from the incoming DTO list
                var incomingIds = dto.Variants.Where(v => v.Id > 0).Select(v => v.Id).ToList();

                // Identify variants in DB that are missing from incoming list
                var variantsToDelete = product.Variants.Where(v => !incomingIds.Contains(v.Id)).ToList();

                foreach (var variantToDelete in variantsToDelete)
                {
                    // Optional: Check if used in transactions before deleting? 
                    // For now, we allow deletion (Standard Soft Delete pattern typically handled by AuditableEntity if implemented)
                    _context.ProductVariants.Remove(variantToDelete);
                }
            }

            // 4. Save Changes
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
