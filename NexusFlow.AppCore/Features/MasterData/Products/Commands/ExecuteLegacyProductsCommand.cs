using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
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
        private readonly IJournalService _journalService; // INJECTED
        private readonly IFinancialAccountResolver _accountResolver;

        public ExecuteLegacyProductsHandler(IFinancialAccountResolver accountResolver, IErpDbContext context, IJournalService journalService)
        {
            _context = context;
            _journalService = journalService;
            _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(ExecuteLegacyProductsCommand request, CancellationToken cancellationToken)
        {
            if (!request.Products.Any()) return Result<int>.Failure("No data to import.");

            // 1. TIER-1 GUARD: Resolve Opening Balance Equity Account
            var obeAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Equity.OpeningBalance", cancellationToken);
            if (obeAccountId == 0)
                return Result<int>.Failure("CRITICAL: System Config 'Account.Equity.OpeningBalance' is missing. Cannot migrate financially.");

            int variantsImported = 0;
            int layersCreated = 0;
            decimal totalCutoverValue = 0;
            var glGroupings = new Dictionary<int, decimal>(); // Tracks Inventory GL balances
            var stockTransactions = new List<StockTransaction>(); // Tracks inventory audit trail

            string migrationRef = $"MIG-INV-{DateTime.UtcNow:yyyyMMddHHmmss}";

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                var existingBrands = await _context.Brands.ToDictionaryAsync(b => b.Name.ToUpper(), b => b, cancellationToken);
                var existingCategories = await _context.Categories.ToDictionaryAsync(c => c.Name.ToUpper(), c => c, cancellationToken);

                var defaultUom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Symbol == "PCS", cancellationToken) ?? new UnitOfMeasure { Name = "Pieces", Symbol = "PCS" };
                if (defaultUom.Id == 0) _context.UnitOfMeasures.Add(defaultUom);

                var defaultWarehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Name == "Main Warehouse", cancellationToken) ?? new Warehouse { Name = "Main Warehouse", Location = "HQ" };
                if (defaultWarehouse.Id == 0) _context.Warehouses.Add(defaultWarehouse);

                var groupedProducts = request.Products
                    .Where(r => !string.IsNullOrWhiteSpace(r.SKU))
                    .GroupBy(r => !string.IsNullOrWhiteSpace(r.LotNo) ? r.LotNo.Trim() : r.ItemName.Trim())
                    .ToList();

                foreach (var group in groupedProducts)
                {
                    var baseRecord = group.First();
                    string parentProductName = group.Key;

                    // A. Resolve Brand
                    string brandName = string.IsNullOrWhiteSpace(baseRecord.Brand) ? "UNBRANDED" : baseRecord.Brand.Trim().ToUpper();
                    if (!existingBrands.TryGetValue(brandName, out var brand))
                    {
                        brand = new Brand { Name = brandName };
                        _context.Brands.Add(brand);
                        existingBrands[brandName] = brand;
                    }

                    // B. Resolve Categories
                    string parentCatName = string.IsNullOrWhiteSpace(baseRecord.Category) ? "UNCATEGORIZED" : baseRecord.Category.Trim().ToUpper();
                    if (!existingCategories.TryGetValue(parentCatName, out var parentCategory))
                    {
                        parentCategory = new Category { Name = parentCatName, Code = parentCatName, CogsAccountId = 5001, InventoryAccountId = 1003 };
                        _context.Categories.Add(parentCategory);
                        existingCategories[parentCatName] = parentCategory;
                    }

                    Category targetCategory = parentCategory;

                    if (!string.IsNullOrWhiteSpace(baseRecord.SubCategory))
                    {
                        string subCatName = baseRecord.SubCategory.Trim().ToUpper();
                        if (!existingCategories.TryGetValue(subCatName, out var subCategory))
                        {
                            subCategory = new Category { Name = subCatName, Code = subCatName, ParentCategory = parentCategory, CogsAccountId = 5001, InventoryAccountId = 1003 };
                            _context.Categories.Add(subCategory);
                            existingCategories[subCatName] = subCategory;
                        }
                        targetCategory = subCategory;
                    }

                    // C. Resolve Product
                    var product = await _context.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Name == parentProductName, cancellationToken);
                    if (product == null)
                    {
                        product = new Product { Name = parentProductName, Description = "Imported via Wizard", Brand = brand, Category = targetCategory, UnitOfMeasure = defaultUom, Type = ProductType.FinishedGood };
                        _context.Products.Add(product);
                    }
                    else if (product.Category == null) product.Category = targetCategory;

                    // D. Map Variants & Create Stock
                    foreach (var record in group)
                    {
                        var variant = product.Variants.FirstOrDefault(v => v.SKU == record.SKU);
                        bool isNewVariant = false;

                        if (variant == null)
                        {
                            if (await _context.ProductVariants.AnyAsync(v => v.SKU == record.SKU, cancellationToken)) continue;

                            variant = new ProductVariant
                            {
                                SKU = record.SKU,
                                Name = record.ItemName,
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

                        // E. CREATE PHYSICAL FIFO & FINANCIAL RECORDS
                        if (record.TotalQuantity > 0)
                        {
                            bool layerExists = !isNewVariant && await _context.StockLayers.AnyAsync(l => l.ProductVariant.SKU == variant.SKU && l.BatchNo == "OB-LEGACY", cancellationToken);

                            if (!layerExists)
                            {
                                // 1. The FIFO Layer
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

                                // 2. The Audit Trail Transaction
                                decimal lineValue = record.TotalQuantity * record.AverageCost;
                                stockTransactions.Add(new StockTransaction
                                {
                                    Date = DateTime.UtcNow,
                                    ProductVariant = variant,
                                    Warehouse = defaultWarehouse,
                                    Type = StockTransactionType.Receipt,
                                    Qty = record.TotalQuantity,
                                    UnitCost = record.AverageCost,
                                    TotalValue = lineValue,
                                    ReferenceDocNo = migrationRef,
                                    Notes = "Legacy Opening Balance"
                                });

                                // 3. Aggregate Financials for the GL
                                int invAccountId = targetCategory.InventoryAccountId ?? 0;
                                if (!glGroupings.ContainsKey(invAccountId)) glGroupings[invAccountId] = 0;
                                glGroupings[invAccountId] += lineValue;
                                totalCutoverValue += lineValue;
                            }
                        }
                    }
                }

                // We must save changes here so Variants get IDs before we save Transactions
                await _context.SaveChangesAsync(cancellationToken);

                if (stockTransactions.Any())
                {
                    await _context.StockTransactions.AddRangeAsync(stockTransactions, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);

                    // F. POST THE MASSIVE GL CUTOVER JOURNAL
                    var journalLines = new List<JournalLineRequest>();

                    // Debit the Inventory Asset Accounts
                    foreach (var grouping in glGroupings.Where(g => g.Value > 0))
                    {
                        journalLines.Add(new JournalLineRequest { AccountId = grouping.Key, Debit = grouping.Value, Credit = 0, Note = "Inventory Opening Balance" });
                    }

                    // Credit the Opening Balance Equity Account
                    journalLines.Add(new JournalLineRequest { AccountId = obeAccountId, Debit = 0, Credit = totalCutoverValue, Note = "Offset for Inventory Cutover" });

                    var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                    {
                        Date = DateTime.UtcNow,
                        Description = $"Inventory Migration Cutover",
                        Module = "Inventory",
                        ReferenceNo = migrationRef,
                        Lines = journalLines
                    });

                    if (!journalResult.Succeeded) throw new Exception($"GL Post Failed: {journalResult.Message}");
                }

                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(variantsImported, $"Imported {variantsImported} items, created {layersCreated} layers. Financial Cutover Value: {totalCutoverValue:C}.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Import Failed: {ex.Message}");
            }
        }
    }
}
