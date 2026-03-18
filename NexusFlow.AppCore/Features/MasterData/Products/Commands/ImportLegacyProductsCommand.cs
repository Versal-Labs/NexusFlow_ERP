using CsvHelper;
using CsvHelper.Configuration;
using MediatR;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Commands
{
    public class ImportLegacyProductsCommand : IRequest<Result<int>>
    {
        public Stream CsvStream { get; set; }

        public ImportLegacyProductsCommand(Stream csvStream)
        {
            CsvStream = csvStream;
        }
    }

    public class ImportLegacyProductsHandler : IRequestHandler<ImportLegacyProductsCommand, Result<int>>
    {
        private readonly IErpDbContext _context;

        public ImportLegacyProductsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> Handle(ImportLegacyProductsCommand request, CancellationToken cancellationToken)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null, // Ignore missing optional columns safely
                HeaderValidated = null
            };

            using var reader = new StreamReader(request.CsvStream);
            using var csv = new CsvReader(reader, config);

            var records = csv.GetRecords<LegacyProductCsvRecord>().ToList();
            if (!records.Any()) return Result<int>.Failure("The CSV file is empty.");

            int variantsImported = 0;

            // 1. Resolve Global Lookups (Avoid querying the DB inside loops)
            var existingBrands = await _context.Brands.ToDictionaryAsync(b => b.Name.ToUpper(), b => b);
            var existingCategories = await _context.Categories.ToDictionaryAsync(c => c.Name.ToUpper(), c => c);
            var defaultUom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Symbol == "NOS" || u.Symbol == "PCS")
                             ?? new UnitOfMeasure { Name = "Pieces", Symbol = "PCS" };

            if (defaultUom.Id == 0) _context.UnitOfMeasures.Add(defaultUom);

            // 2. Group records by ItemName to establish Parent-Child Variant relationships
            var groupedProducts = records
                .Where(r => !string.IsNullOrWhiteSpace(r.ItemName) && !string.IsNullOrWhiteSpace(r.ItemCode))
                .GroupBy(r => r.ItemName!.Trim())
                .ToList();

            foreach (var group in groupedProducts)
            {
                var baseRecord = group.First();

                // Resolve Brand
                var brandName = string.IsNullOrWhiteSpace(baseRecord.Brand) ? "UNBRANDED" : baseRecord.Brand.Trim().ToUpper();
                if (!existingBrands.TryGetValue(brandName, out var brand))
                {
                    brand = new Brand { Name = brandName };
                    _context.Brands.Add(brand);
                    existingBrands[brandName] = brand; // Cache for next iteration
                }

                // Resolve Category (Flattening their weird type/type1 structure for this import)
                var catName = string.IsNullOrWhiteSpace(baseRecord.Type) ? "UNCATEGORIZED" : baseRecord.Type.Trim().ToUpper();
                if (!existingCategories.TryGetValue(catName, out var category))
                {
                    // Remember: In a real flow, you'd map GL accounts here.
                    category = new Category { Name = catName, Code = catName };
                    _context.Categories.Add(category);
                    existingCategories[catName] = category;
                }

                // Check if Parent Product already exists
                var product = await _context.Products
                    .Include(p => p.Variants)
                    .FirstOrDefaultAsync(p => p.Name == baseRecord.ItemName, cancellationToken);

                if (product == null)
                {
                    product = new Product
                    {
                        Name = baseRecord.ItemName!,
                        Description = "Imported from Legacy System",
                        Brand = brand,
                        Category = category,
                        UnitOfMeasure = defaultUom,
                        Type = ProductType.FinishedGood // Defaulting to FG.
                    };
                    _context.Products.Add(product);
                }

                // Append Variants
                foreach (var record in group)
                {
                    // Skip if SKU already exists
                    if (product.Variants.Any(v => v.SKU == record.ItemCode) ||
                        await _context.ProductVariants.AnyAsync(v => v.SKU == record.ItemCode, cancellationToken))
                    {
                        continue;
                    }

                    var variantSize = !string.IsNullOrWhiteSpace(record.Size) ? record.Size : record.MSize ?? "N/A";
                    var variantColor = !string.IsNullOrWhiteSpace(record.Color) ? record.Color : "N/A";

                    product.Variants.Add(new ProductVariant
                    {
                        SKU = record.ItemCode!,
                        Name = $"{product.Name} - {variantSize} - {variantColor}",
                        Size = variantSize,
                        Color = variantColor,
                        CostPrice = record.AverageCost,
                        MovingAverageCost = record.AverageCost, // Populating the client requirement!
                        SellingPrice = record.SellingPrice,
                        MinimumSellingPrice = record.MinSellingPrice,
                        IsActive = true
                    });

                    variantsImported++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(variantsImported, $"{variantsImported} product variants imported successfully.");
        }
    }
}
