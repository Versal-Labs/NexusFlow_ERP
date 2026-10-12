using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Features.Inventory.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;

namespace NexusFlow.IntegrationTests;

public class RawMaterialImportTests : TestBase
{
    private const string Csv = "ProductName,SKU,Quantity,UnitCost\r\n\"Tag, black\",RM-NEW-1,10,2.50\r\n\"Tag, black\",RM-NEW-2,1,1\r\n";

    private static ImportRawMaterialStockCommand Command(string csv = Csv, bool execute = false, string? hash = null) => new()
    {
        CsvStream = new MemoryStream(Encoding.UTF8.GetBytes(csv)), OpeningDate = new DateTime(2026, 9, 15),
        WarehouseCode = "MAIN", BrandName = "Test Brand", Execute = execute, PreviewHash = hash
    };

    [Fact]
    public async Task Preview_IsReadOnly_AndShowsResolvedAccountsAndTotals()
    {
        var result = await SendAsync(Command());
        result.Succeeded.Should().BeTrue(result.Message);
        result.Data.CanImport.Should().BeTrue(string.Join(";", result.Data.Errors));
        result.Data.TotalQuantity.Should().Be(11);
        result.Data.TotalValue.Should().Be(26);
        result.Data.CreatesCategory.Should().BeTrue();
        result.Data.InventoryAccount.Should().StartWith("1031");
        result.Data.EquityAccount.Should().StartWith("3200");
        result.Data.Warnings.Should().Contain(x => x.Contains("equal to 1"));
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await db.Products.CountAsync()).Should().Be(2);
        (await db.StockLayers.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.Categories.CountAsync(x => x.Name == "Accessories")).Should().Be(0);
    }

    [Fact]
    public async Task Execute_CreatesRawMaterialsFifoStockLedgerAndBalancedJournal_AndRejectsReplay()
    {
        var preview = await SendAsync(Command());
        var result = await SendAsync(Command(execute: true, hash: preview.Data.PreviewHash));
        result.Succeeded.Should().BeTrue(result.Message);
        result.Data.ReferenceNo.Should().StartWith("OBSTK-");
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        var variants = await db.ProductVariants.Include(x => x.Product).Where(x => x.SKU.StartsWith("RM-NEW-")).ToListAsync();
        variants.Should().HaveCount(2);
        variants.Select(x => x.ProductId).Distinct().Should().HaveCount(2, "equal names must not merge separate SKUs");
        variants.Should().OnlyContain(x => x.Product.Type == ProductType.RawMaterial && x.SellingPrice == 0 && x.MinimumSellingPrice == 0);
        variants.Should().OnlyContain(x => x.CostPrice == x.MovingAverageCost);
        var layers = await db.StockLayers.ToListAsync();
        layers.Should().HaveCount(2);
        layers.Should().OnlyContain(x => x.WarehouseId == 1 && x.InitialQty == x.RemainingQty && !x.IsExhausted);
        layers.Should().OnlyContain(x => x.DateReceived == new DateTime(2026, 9, 15) && x.BatchNo == result.Data.ReferenceNo);
        var transactions = await db.StockTransactions.ToListAsync();
        transactions.Should().OnlyContain(x => x.Type == StockTransactionType.OpeningBalance && x.ReferenceDocNo == result.Data.ReferenceNo);
        transactions.Sum(x => x.TotalValue).Should().Be(26);
        var journal = await db.JournalEntries.Include(x => x.Lines).SingleAsync();
        journal.Lines.Should().HaveCount(2);
        journal.Lines.Single(x => x.AccountId == 1031).Debit.Should().Be(26);
        journal.Lines.Single(x => x.AccountId == 3200).Credit.Should().Be(26);
        (await db.Accounts.SingleAsync(x => x.Id == 1031)).Balance.Should().Be(26);
        (await db.Accounts.SingleAsync(x => x.Id == 3200)).Balance.Should().Be(-26);
        var replay = await SendAsync(Command(execute: true, hash: preview.Data.PreviewHash));
        replay.Succeeded.Should().BeFalse();
        replay.Message.Should().Contain("already exists");
        (await db.StockTransactions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Execute_RequiresFreshPreview_AndDoesNotCreateAnythingWhenStale()
    {
        var preview = await SendAsync(Command());
        var noPreview = await SendAsync(Command(execute: true));
        noPreview.Succeeded.Should().BeFalse();
        var changed = await SendAsync(Command(Csv.Replace(",10,", ",20,"), true, preview.Data.PreviewHash));
        changed.Succeeded.Should().BeFalse();
        changed.Message.Should().Contain("Preview again");
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await db.Products.CountAsync()).Should().Be(2);
        (await db.Categories.CountAsync(x => x.Name == "Accessories")).Should().Be(0);
    }

    [Fact]
    public async Task ExistingCategoryWithWrongMappings_IsNotChanged()
    {
        var command = Command(); command.CategoryName = "Finished Good";
        var result = await SendAsync(command);
        result.Data.CanImport.Should().BeFalse();
        result.Data.Errors.Should().Contain(x => x.Contains("Existing category mappings differ"));
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await db.Categories.SingleAsync(x => x.Code == "FG")).InventoryAccountId.Should().Be(1032);
    }

    [Theory]
    [InlineData("warehouse")]
    [InlineData("brand")]
    [InlineData("unit")]
    [InlineData("account")]
    [InlineData("mapping")]
    public async Task MissingOrInactiveSetup_BlocksImport(string problem)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
            if (problem == "warehouse") (await db.Warehouses.SingleAsync(x => x.Id == 1)).IsActive = false;
            if (problem == "brand") (await db.Brands.FirstAsync()).Name = "Other brand";
            if (problem == "unit") (await db.UnitOfMeasures.FirstAsync()).Symbol = "M";
            if (problem == "account") (await db.Accounts.SingleAsync(x => x.Id == 1031)).IsActive = false;
            if (problem == "mapping") db.SystemConfigs.Remove(await db.SystemConfigs.SingleAsync(x => x.Key == "Account.Equity.OpeningBalance"));
            await db.SaveChangesAsync(CancellationToken.None);
        }
        var preview = await SendAsync(Command());
        preview.Data.CanImport.Should().BeFalse();
        var result = await SendAsync(Command(execute: true, hash: preview.Data.PreviewHash));
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ClosedPeriod_BlocksBeforeWriting()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
            (await db.FinancialPeriods.FirstAsync()).IsClosed = true;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        var result = await SendAsync(Command(execute: true, hash: "not-used"));
        result.Succeeded.Should().BeFalse();
        result.Code.Should().Be("financial_period_not_open");
    }

    [Theory]
    [InlineData("ProductName,SKU,Quantity,UnitCost\nTag,X,0,1", "Quantity")]
    [InlineData("ProductName,SKU,Quantity,UnitCost\nTag,X,1.5,1", "Quantity")]
    [InlineData("ProductName,SKU,Quantity,UnitCost\nTag,X,-1,1", "Quantity")]
    [InlineData("ProductName,SKU,Quantity,UnitCost\nTag,X,1,0", "UnitCost")]
    [InlineData("ProductName,SKU,Quantity,UnitCost\nTag,X,1,NaN", "UnitCost")]
    [InlineData("ProductName,SKU,Quantity,UnitCost\nTag,X,1,0.001", "UnitCost")]
    [InlineData("ProductName,SKU,Quantity,UnitCost\nTag,X,1,1\nTag,x,2,2", "Duplicate SKU")]
    [InlineData("ProductName,Quantity,UnitCost\nTag,1,1", "Missing CSV column")]
    [InlineData("ProductName,SKU,SKU,Quantity,UnitCost", "Duplicate CSV headers")]
    [InlineData("ProductName,SKU,Quantity,UnitCost\nTag,X,1,1,extra", "Malformed CSV")]
    [InlineData("ProductName,SKU,Quantity,UnitCost\nTag,X,99999999999999,2", "limits")]
    public void Parser_RejectsInvalidRows(string csv, string error)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var preview = new RawMaterialImportPreview();
        ImportRawMaterialStockHandler.Parse(stream, preview);
        preview.CanImport.Should().BeFalse();
        preview.Errors.Should().Contain(x => x.Contains(error));
    }

    [Fact]
    public void Parser_AcceptsBomQuotedMultilineNames_AndWarnsAboutIgnoredColumns()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
            "ProductGroup,ProductName,SKU,Quantity,UnitCost,Category\r\n,\"Tag, black\r\nLarge\",A001,12,2.50,ACCOSORIES NEW\r\n")).ToArray());
        var preview = new RawMaterialImportPreview();
        ImportRawMaterialStockHandler.Parse(stream, preview);
        preview.CanImport.Should().BeTrue();
        preview.Rows.Single().SKU.Should().Be("A001");
        preview.TotalValue.Should().Be(30);
        preview.Warnings.Should().Contain(x => x.Contains("ignored"));
    }

    [Fact]
    public void Parser_RejectsOverLimitFile()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("ProductName,SKU,Quantity,UnitCost\n" +
            string.Join("\n", Enumerable.Range(1, 2001).Select(x => $"Tag,SKU-{x},1,1"))));
        var preview = new RawMaterialImportPreview();
        ImportRawMaterialStockHandler.Parse(stream, preview);
        preview.Errors.Should().Contain(x => x.Contains("2,000"));
    }
}
