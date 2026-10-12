using System.Globalization;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Inventory.Commands;

public sealed class ImportRawMaterialStockCommand : IRequest<Result<RawMaterialImportPreview>>, IFinancialPeriodControlledRequest
{
    public Stream CsvStream { get; set; } = Stream.Null;
    public DateTime OpeningDate { get; set; }
    public string WarehouseCode { get; set; } = "WH-MAIN";
    public string BrandName { get; set; } = "UNBRANDED";
    public string CategoryName { get; set; } = "Accessories";
    public bool Execute { get; set; }
    public string? PreviewHash { get; set; }
    public DateTime FinancialDate => OpeningDate;
}

public sealed record RawMaterialImportRow(int Row, string ProductName, string SKU, decimal Quantity, decimal UnitCost)
{
    public decimal Value => Quantity * UnitCost;
}

public sealed class RawMaterialImportPreview
{
    public List<RawMaterialImportRow> Rows { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public decimal TotalQuantity => Rows.Sum(x => x.Quantity);
    public decimal TotalValue => Rows.Sum(x => x.Value);
    public string Warehouse { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "PCS";
    public DateTime OpeningDate { get; set; }
    public bool CreatesCategory { get; set; }
    public string InventoryAccount { get; set; } = "";
    public string EquityAccount { get; set; } = "";
    public string CostAccount { get; set; } = "";
    public string SalesAccount { get; set; } = "";
    public string PreviewHash { get; set; } = "";
    public string? ReferenceNo { get; set; }
    public bool CanImport => Errors.Count == 0 && Rows.Count > 0;
}

public sealed class ImportRawMaterialStockHandler(IErpDbContext db, INumberSequenceService sequences,
    IJournalService journals, ILogger<ImportRawMaterialStockHandler> logger) : IRequestHandler<ImportRawMaterialStockCommand, Result<RawMaterialImportPreview>>
{
    // Totals fit decimal(18,4); ProductVariant.CostPrice is decimal(18,2).
    private const decimal MaxValue = 99999999999999.9999m;

    public async Task<Result<RawMaterialImportPreview>> Handle(ImportRawMaterialStockCommand request, CancellationToken ct)
    {
        try { return await Process(request, ct); }
        catch (Exception ex) when (ex is DbException or DbUpdateException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Raw material import database operation failed.");
            return Result<RawMaterialImportPreview>.Failure("The database request could not complete (for example, another import may be running). Check Opening Stock before retrying, then preview again.");
        }
    }

    private async Task<Result<RawMaterialImportPreview>> Process(ImportRawMaterialStockCommand request, CancellationToken ct)
    {
        var preview = new RawMaterialImportPreview { OpeningDate = request.OpeningDate.Date };
        Parse(request.CsvStream, preview);
        // Keep execute-time configuration/category/SKU reads stable through commit, including concurrent imports.
        // Do not read NumberSequences here: the existing sequence service reserves numbers on a separate connection.
        await using var transaction = request.Execute
            ? await db.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        if (request.OpeningDate == default) preview.Errors.Add("Opening date is required.");
        var warehouseCode = (request.WarehouseCode ?? "").Trim();
        var brandName = (request.BrandName ?? "").Trim();
        var categoryName = (request.CategoryName ?? "").Trim();
        if (categoryName.Length is < 1 or > 100) preview.Errors.Add("Category name must contain 1–100 characters.");
        var warehouses = await db.Warehouses.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var matchingWarehouses = warehouses.Where(x => x.Code.Equals(warehouseCode, StringComparison.OrdinalIgnoreCase)).ToList();
        var warehouse = matchingWarehouses.Count == 1 ? matchingWarehouses[0] : null;
        var brands = await db.Brands.AsNoTracking().ToListAsync(ct);
        var matchingBrands = brands.Where(x => x.Name.Equals(brandName, StringComparison.OrdinalIgnoreCase)).ToList();
        var brand = matchingBrands.Count == 1 ? matchingBrands[0] : null;
        var units = await db.UnitOfMeasures.AsNoTracking().ToListAsync(ct);
        var matchingUnits = units.Where(x => x.Symbol.Equals("PCS", StringComparison.OrdinalIgnoreCase)).ToList();
        var unit = matchingUnits.Count == 1 ? matchingUnits[0] : null;
        if (warehouse == null) preview.Errors.Add("An active warehouse with the selected code is required.");
        if (brand == null) preview.Errors.Add("Exactly one existing brand with the selected name is required.");
        if (unit == null) preview.Errors.Add("Exactly one existing PCS unit of measure is required.");
        preview.Warehouse = warehouse == null ? warehouseCode : $"{warehouse.Code} — {warehouse.Name}";
        preview.Brand = brandName;
        preview.Category = categoryName;

        // Resolve fresh configuration by account CODE, never by a guessed database ID or cached mapping.
        var inventory = await Resolve("Account.Inventory.RawMaterial", AccountType.Asset, preview, ct);
        var equity = await Resolve("Account.Equity.OpeningBalance", AccountType.Equity, preview, ct);
        var cost = await Resolve("Account.Cost.COGS", AccountType.Expense, preview, ct);
        var sales = await Resolve("Account.Sales.Revenue", AccountType.Revenue, preview, ct);
        preview.InventoryAccount = Label(inventory);
        preview.EquityAccount = Label(equity);
        preview.CostAccount = Label(cost);
        preview.SalesAccount = Label(sales);
        var categories = await db.Categories.AsNoTracking().ToListAsync(ct);
        var matches = categories.Where(x => x.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase)
            || x.Code.Equals(categoryName, StringComparison.OrdinalIgnoreCase)).ToList();
        var category = matches.Count == 1 ? matches[0] : null;
        if (matches.Count > 1) preview.Errors.Add("Category name/code is ambiguous. Select a unique category name.");
        preview.CreatesCategory = matches.Count == 0;
        if (category != null && (category.InventoryAccountId != inventory?.Id || category.CogsAccountId != cost?.Id
            || category.SalesAccountId != sales?.Id))
            preview.Errors.Add("Existing category mappings differ from configured raw-material accounts. Use a new category name or have an administrator review its mappings. No shared category will be modified.");

        var existingSkus = (await db.ProductVariants.AsNoTracking().Select(x => x.SKU).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in preview.Rows.Where(x => existingSkus.Contains(x.SKU)))
            preview.Errors.Add($"Row {row.Row}: SKU '{row.SKU}' already exists. This importer only creates new items; it never replaces or adds stock to existing SKUs.");
        preview.PreviewHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            preview.Rows, preview.OpeningDate, preview.Warehouse, preview.Brand, preview.Category,
            preview.CreatesCategory, preview.InventoryAccount, preview.EquityAccount, preview.CostAccount, preview.SalesAccount,
            WarehouseId = warehouse?.Id, BrandId = brand?.Id, UnitId = unit?.Id, CategoryId = category?.Id,
            InventoryId = inventory?.Id, EquityId = equity?.Id, CostId = cost?.Id, SalesId = sales?.Id
        }))));
        if (!request.Execute) return Result<RawMaterialImportPreview>.Success(preview, "Preview only. Nothing has been saved.");
        if (!preview.CanImport) return Result<RawMaterialImportPreview>.Failure(string.Join("\n", preview.Errors.Take(30)));
        if (!string.Equals(request.PreviewHash, preview.PreviewHash, StringComparison.Ordinal))
            return Result<RawMaterialImportPreview>.Failure("File or setup changed. Preview again before importing.");

        // Catalog, FIFO, stock ledger and GL share ONE transaction. A failed/replayed import cannot leave partial stock.
        try
        {
            if (category == null)
            {
                category = new Category { Name = categoryName, Code = categoryName, InventoryAccountId = inventory!.Id,
                    CogsAccountId = cost!.Id, SalesAccountId = sales!.Id };
                db.Categories.Add(category);
                await db.SaveChangesAsync(ct);
            }
            var reference = await sequences.GenerateNextNumberAsync(NumberSequenceKeys.OpeningStock, ct);
            foreach (var row in preview.Rows)
            {
                var product = new Product { Name = row.ProductName, Type = ProductType.RawMaterial,
                    BrandId = brand!.Id, UnitOfMeasureId = unit!.Id, CategoryId = category.Id,
                    Description = $"Raw material opening stock import {reference}" };
                var variant = new ProductVariant { Product = product, Name = row.ProductName, SKU = row.SKU,
                    CostPrice = row.UnitCost, MovingAverageCost = row.UnitCost, SellingPrice = 0, IsActive = true };
                db.ProductVariants.Add(variant);
                db.StockLayers.Add(new StockLayer { ProductVariant = variant, WarehouseId = warehouse!.Id,
                    BatchNo = reference, DateReceived = request.OpeningDate.Date, UnitCost = row.UnitCost,
                    InitialQty = row.Quantity, RemainingQty = row.Quantity });
                db.StockTransactions.Add(new StockTransaction { ProductVariant = variant, WarehouseId = warehouse.Id,
                    Date = request.OpeningDate.Date, Type = StockTransactionType.OpeningBalance, Qty = row.Quantity,
                    UnitCost = row.UnitCost, TotalValue = row.Value, ReferenceDocNo = reference,
                    Notes = "Raw material CSV opening stock" });
            }
            await db.SaveChangesAsync(ct);
            var journal = await journals.PostJournalAsync(new JournalEntryRequest
            {
                Date = request.OpeningDate.Date, Description = $"Raw material opening stock: {reference}",
                Module = "Inventory", ReferenceNo = reference,
                Lines = [new() { AccountId = inventory!.Id, Debit = preview.TotalValue, Note = "Raw material opening inventory" },
                    new() { AccountId = equity!.Id, Credit = preview.TotalValue, Note = "Opening equity offset" }]
            });
            if (!journal.Succeeded) throw new InvalidOperationException(journal.Message ?? "Journal posting failed.");
            await transaction!.CommitAsync(ct);
            preview.ReferenceNo = reference;
            return Result<RawMaterialImportPreview>.Success(preview, $"Imported {preview.Rows.Count} raw materials. Opening stock {reference} posted.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction!.RollbackAsync(ct);
            logger.LogWarning(ex, "Raw material import rolled back.");
            return Result<RawMaterialImportPreview>.Failure("Import was rolled back. No partial import was committed. Check for existing SKUs, account/period configuration, then preview again. " +
                (ex is DbUpdateException ? "A database constraint prevented saving." : ex.Message));
        }
    }

    private async Task<Account?> Resolve(string key, AccountType type, RawMaterialImportPreview preview, CancellationToken ct)
    {
        var code = await db.SystemConfigs.AsNoTracking().Where(x => x.Key == key).Select(x => x.Value).FirstOrDefaultAsync(ct);
        var account = code == null ? null : await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
        if (account == null || !account.IsActive || !account.IsTransactionAccount || account.Type != type)
            preview.Errors.Add($"{key} must resolve to an active, postable {type} account.");
        return account;
    }

    private static string Label(Account? account) => account == null ? "Missing" : $"{account.Code} — {account.Name}";

    public static void Parse(Stream stream, RawMaterialImportPreview preview)
    {
        try
        {
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true, leaveOpen: true);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                TrimOptions = TrimOptions.Trim, DetectColumnCountChanges = true,
                PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant()
            });
            if (!csv.Read()) { preview.Errors.Add("CSV is empty."); return; }
            csv.ReadHeader();
            var headers = csv.HeaderRecord!.Select(x => x.Trim().ToLowerInvariant()).ToArray();
            if (headers.Distinct().Count() != headers.Length) { preview.Errors.Add("Duplicate CSV headers are not allowed."); return; }
            foreach (var required in new[] { "productname", "sku", "quantity", "unitcost" })
                if (!headers.Contains(required)) preview.Errors.Add($"Missing CSV column: {required}.");
            if (preview.Errors.Count > 0) return;
            if (headers.Except(new[] { "productname", "sku", "quantity", "unitcost" }).Any())
                preview.Warnings.Add("Only ProductName, SKU, Quantity and UnitCost are imported from the CSV. All other columns (including category, brand, unit, warehouse, date and ProductGroup) are ignored. The setup shown here applies to EVERY row.");
            var skus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var count = 0;
            decimal total = 0, totalQty = 0;
            while (csv.Read())
            {
                if (++count > 2000) { preview.Errors.Add("Maximum 2,000 rows per import."); break; }
                var rowNumber = csv.Parser.Row;
                var name = csv.GetField("productname")?.Trim() ?? "";
                var sku = csv.GetField("sku")?.Trim() ?? "";
                var valid = true;
                void Error(string message) { valid = false; preview.Errors.Add($"Row {rowNumber}: {message}"); }
                if (name.Length is < 1 or > 200) Error("ProductName must contain 1–200 characters.");
                if (sku.Length is < 1 or > 50) Error("SKU must contain 1–50 characters.");
                if (!skus.Add(sku)) Error($"Duplicate SKU '{sku}'.");
                if (!decimal.TryParse(csv.GetField("quantity"), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var qty) || qty <= 0 || qty % 1 != 0 || qty > MaxValue)
                    Error("Quantity must be a positive whole number without thousands separators.");
                if (!decimal.TryParse(csv.GetField("unitcost"), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var cost) || cost <= 0 || cost > MaxValue || decimal.Round(cost, 2) != cost)
                    Error("UnitCost must be positive with at most 2 decimal places and no thousands separators.");
                if (!valid) continue;
                if (cost > MaxValue / qty || total > MaxValue - qty * cost || totalQty > MaxValue - qty)
                { Error("Quantity/value exceeds supported accounting limits."); continue; }
                total += qty * cost; totalQty += qty;
                preview.Rows.Add(new(rowNumber, name, sku, qty, cost));
            }
            if (count == 0) preview.Errors.Add("CSV has no data rows.");
            var ones = preview.Rows.Count(x => x.Quantity == 1 || x.UnitCost == 1);
            if (ones > 0) preview.Warnings.Add($"{ones} rows have quantity or unit cost equal to 1. Confirm these are real stock figures, not placeholders.");
            preview.Warnings.Add("Each SKU becomes a separate Raw Material (Non-Sellable Stock) item, even when names repeat. Selling price is zero. Stock is debited to inventory and credited to opening equity; no sale or consumption expense is posted now.");
        }
        catch (Exception ex) when (ex is CsvHelperException or DecoderFallbackException)
        { preview.Errors.Add("Malformed CSV. Check quoting, UTF-8 encoding and column counts. " + ex.GetType().Name); }
    }
}
