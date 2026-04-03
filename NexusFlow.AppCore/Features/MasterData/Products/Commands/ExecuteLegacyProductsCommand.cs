using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Inventory;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Commands
{
    public class ExecuteLegacyProductsCommand : IRequest<Result<int>>
    {
        public List<ProductImportDto> Products { get; set; } = new();
    }

    public class ExecuteLegacyProductsHandler : IRequestHandler<ExecuteLegacyProductsCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public ExecuteLegacyProductsHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(ExecuteLegacyProductsCommand request, CancellationToken cancellationToken)
        {
            if (!request.Products.Any()) return Result<int>.Failure("No data to import.");

            int variantsImported = 0;
            int layersCreated = 0;
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Resolve Global Master Data
                var existingBrands = await _context.Brands.ToDictionaryAsync(b => b.Name.ToUpper(), b => b, cancellationToken);
                var existingCategories = await _context.Categories.ToDictionaryAsync(c => c.Name.ToUpper(), c => c, cancellationToken);
                var defaultUom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Symbol == "PCS", cancellationToken)
                                 ?? new UnitOfMeasure { Name = "Pieces", Symbol = "PCS" };

                if (defaultUom.Id == 0) _context.UnitOfMeasures.Add(defaultUom);

                // 2. Resolve Default Warehouse for Opening Balances
                var defaultWarehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Name == "Main Warehouse", cancellationToken)
                                      ?? new Warehouse { Name = "Main Warehouse", Location = "HQ" };

                if (defaultWarehouse.Id == 0) _context.Warehouses.Add(defaultWarehouse);

                // ==========================================
                // 3. GROUP BY LOT NO (Parent) -> ItemName (Variant)
                // ==========================================
                var groupedProducts = request.Products
                    .Where(r => !string.IsNullOrWhiteSpace(r.SKU))
                    .GroupBy(r => !string.IsNullOrWhiteSpace(r.LotNo) ? r.LotNo.Trim() : r.ItemName.Trim())
                    .ToList();

                foreach (var group in groupedProducts)
                {
                    var baseRecord = group.First();
                    string parentProductName = group.Key; // e.g., "345 SHIRT"

                    // A. Resolve Brand
                    string brandName = string.IsNullOrWhiteSpace(baseRecord.Brand) ? "UNBRANDED" : baseRecord.Brand.Trim().ToUpper();
                    if (!existingBrands.TryGetValue(brandName, out var brand))
                    {
                        brand = new Brand { Name = brandName };
                        _context.Brands.Add(brand);
                        existingBrands[brandName] = brand;
                    }

                    // B. Resolve Hierarchy & HARDCODE GL ACCOUNTS
                    string parentCatName = string.IsNullOrWhiteSpace(baseRecord.Category) ? "UNCATEGORIZED" : baseRecord.Category.Trim().ToUpper();

                    // 1. Parent Category (Gents, Ladies, etc.)
                    if (!existingCategories.TryGetValue(parentCatName, out var parentCategory))
                    {
                        parentCategory = new Category
                        {
                            Name = parentCatName,
                            Code = parentCatName,
                            CogsAccountId = 5001,       // GL MAPPING
                            InventoryAccountId = 1003   // GL MAPPING
                        };
                        _context.Categories.Add(parentCategory);
                        existingCategories[parentCatName] = parentCategory;
                    }

                    Category targetCategoryForProduct = parentCategory;

                    // 2. Sub Category (REGULAR FIT, SLIM FIT, etc.)
                    if (!string.IsNullOrWhiteSpace(baseRecord.SubCategory))
                    {
                        string subCatName = baseRecord.SubCategory.Trim().ToUpper();

                        if (!existingCategories.TryGetValue(subCatName, out var subCategory))
                        {
                            subCategory = new Category
                            {
                                Name = subCatName,
                                Code = subCatName,
                                ParentCategory = parentCategory, // HIERARCHY ESTABLISHED
                                CogsAccountId = 5001,            // GL MAPPING
                                InventoryAccountId = 1003        // GL MAPPING
                            };
                            _context.Categories.Add(subCategory);
                            existingCategories[subCatName] = subCategory;
                        }

                        targetCategoryForProduct = subCategory;
                    }

                    // C. Resolve Parent Product
                    var product = await _context.Products
                        .Include(p => p.Variants)
                        .FirstOrDefaultAsync(p => p.Name == parentProductName, cancellationToken);

                    if (product == null)
                    {
                        product = new Product
                        {
                            Name = parentProductName, // Assigned via LotNo Grouping Key
                            Description = "Imported via Wizard",
                            Brand = brand,
                            Category = targetCategoryForProduct, // Links to Leaf Node
                            UnitOfMeasure = defaultUom,
                            Type = ProductType.FinishedGood
                        };
                        _context.Products.Add(product);
                    }
                    else if (product.Category == null)
                    {
                        product.Category = targetCategoryForProduct;
                    }

                    // D. Map Variants and Create FIFO Stock Layers
                    foreach (var record in group)
                    {
                        var variant = product.Variants.FirstOrDefault(v => v.SKU == record.SKU);
                        bool isNewVariant = false;

                        if (variant == null)
                        {
                            bool existsInDb = await _context.ProductVariants.AnyAsync(v => v.SKU == record.SKU, cancellationToken);
                            if (existsInDb) continue;

                            variant = new ProductVariant
                            {
                                SKU = record.SKU,
                                Name = record.ItemName, // Specifically the variant name from CSV (e.g. "SHIRT S/F 345 L/S 16H")
                                Size = record.Size,
                                Color = record.Color,
                                CostPrice = record.AverageCost,
                                MovingAverageCost = record.AverageCost,
                                SellingPrice = record.SellingPrice,
                                MinimumSellingPrice = record.MinSellingPrice,
                                IsActive = true
                            };
                            product.Variants.Add(variant);
                            isNewVariant = true;
                            variantsImported++;
                        }

                        // Create FIFO Opening Balance
                        if (record.TotalQuantity > 0)
                        {
                            bool layerExists = !isNewVariant && await _context.StockLayers
                                .AnyAsync(l => l.ProductVariant.SKU == variant.SKU && l.BatchNo == "OB-LEGACY", cancellationToken);

                            if (!layerExists)
                            {
                                var stockLayer = new StockLayer
                                {
                                    ProductVariant = variant,
                                    Warehouse = defaultWarehouse,
                                    BatchNo = "OB-LEGACY",
                                    DateReceived = DateTime.UtcNow,
                                    UnitCost = record.AverageCost,
                                    InitialQty = record.TotalQuantity,
                                    RemainingQty = record.TotalQuantity,
                                    IsExhausted = false
                                };

                                _context.StockLayers.Add(stockLayer);
                                layersCreated++;
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(variantsImported,
                    $"Success! Imported {variantsImported} variants and created {layersCreated} FIFO Stock Layers in Main Warehouse.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Import Failed: {ex.Message}");
            }
        }
    }
}
