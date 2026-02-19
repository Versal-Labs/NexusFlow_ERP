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
    // DTO for the list view (lighter than the full entity)
    

    public class GetProductsHandler : IRequestHandler<GetProductsQuery, Result<List<ProductDto>>>
    {
        private readonly IErpDbContext _context;

        public GetProductsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<ProductDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
        {
            // 1. Query the PARENT 'Products' table (Not ProductVariants)
            var query = _context.Products
                .AsNoTracking()
                // 2. Include Related Master Data
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.UnitOfMeasure)
                .Include(p => p.Variants)
                // 3. Include Financial Accounts (For the "Account Info" column)
                .Include(p => p.SalesAccount)
                .Include(p => p.CogsAccount)
                .Include(p => p.InventoryAccount)
                .OrderBy(p => p.Name);

            // 4. Project to DTO (Flattening relations for DataTables)
            var products = await query.Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Type = p.Type, // Enum

                // --- IDs (Required for "Edit" Mode to set Dropdowns) ---
                BrandId = p.BrandId,
                CategoryId = p.CategoryId,
                UnitOfMeasureId = p.UnitOfMeasureId,
                SalesAccountId = p.SalesAccountId,
                CogsAccountId = p.CogsAccountId,
                InventoryAccountId = p.InventoryAccountId,

                // --- Names (Required for DataGrid Display) ---
                // This fixes "unknown parameter 'categoryName'"
                CategoryName = p.Category.Name,
                BrandName = p.Brand.Name,
                UnitName = p.UnitOfMeasure.Symbol,

                // --- Financial Names ---
                SalesAccountName = p.SalesAccount.Name,
                CogsAccountName = p.CogsAccount.Name,
                // Handle Nullable Inventory Account (Services don't have one)
                InventoryAccountName = p.InventoryAccount != null ? p.InventoryAccount.Name : "-",

                Variants = p.Variants.Select(v => new ProductVariantDto
                {
                    Id = v.Id,
                    SKU = v.SKU,
                    Size = v.Size,
                    Color = v.Color,
                    SellingPrice = v.SellingPrice,
                    CostPrice = v.CostPrice,
                    ReorderLevel = v.ReorderLevel
                }).ToList()
            })
            .ToListAsync(cancellationToken);

            return Result<List<ProductDto>>.Success(products);
        }
    }
}
