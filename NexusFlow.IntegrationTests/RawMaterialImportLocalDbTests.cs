using System.Text;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Features.Inventory.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.IntegrationTests;

public sealed class RawMaterialLocalDbFactAttribute : FactAttribute
{
    public RawMaterialLocalDbFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("NEXUSFLOW_RUN_LOCALDB_IMPORT_TESTS") != "1")
            Skip = "Opt-in: set NEXUSFLOW_RUN_LOCALDB_IMPORT_TESTS=1 on Windows with MSSQLLocalDB.";
    }
}

public class RawMaterialImportLocalDbTests
{
    private static ImportRawMaterialStockCommand Command(string? hash = null, bool execute = false) => new()
    {
        CsvStream = new MemoryStream(Encoding.UTF8.GetBytes("ProductName,SKU,Quantity,UnitCost\nTag,RM-NEW-1,10,2.50\nButton,RM-NEW-2,1,1\n")),
        OpeningDate = new DateTime(2026, 9, 15), PreviewHash = hash, Execute = execute
    };
    private static async Task<Result<RawMaterialImportPreview>> Send(IServiceProvider services, ImportRawMaterialStockCommand command)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    [RawMaterialLocalDbFact]
    public async Task RealSqlServer_RollsBackCatalogStockAndCategory_WhenJournalFails()
    {
        await using var database = new RawMaterialImportTestDatabase();
        await database.Initialize();
        var services = new ServiceCollection(); database.Configure(services);
        var journal = new Mock<IJournalService>();
        journal.Setup(x => x.PostJournalAsync(It.IsAny<JournalEntryRequest>())).ReturnsAsync(Result<int>.Failure("Injected journal failure"));
        services.AddSingleton(journal.Object);
        await using var provider = services.BuildServiceProvider();
        var preview = await Send(provider, Command());
        preview.Data.CanImport.Should().BeTrue(string.Join(";", preview.Data.Errors));
        var result = await Send(provider, Command(preview.Data.PreviewHash, true));
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("rolled back");
        await using var db = database.Open();
        (await db.Products.CountAsync()).Should().Be(0);
        (await db.ProductVariants.CountAsync()).Should().Be(0);
        (await db.StockLayers.CountAsync()).Should().Be(0);
        (await db.StockTransactions.CountAsync()).Should().Be(0);
        (await db.Categories.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.Accounts.ToListAsync()).Should().OnlyContain(x => x.Balance == 0);
    }

    [RawMaterialLocalDbFact]
    public async Task RealSqlServer_PersistsCorrectDecimalsAndBalances_AndRejectsRepeat()
    {
        await using var database = new RawMaterialImportTestDatabase();
        await database.Initialize();
        var services = new ServiceCollection(); database.Configure(services);
        await using var provider = services.BuildServiceProvider();
        var preview = await Send(provider, Command());
        var result = await Send(provider, Command(preview.Data.PreviewHash, true));
        result.Succeeded.Should().BeTrue(result.Message);
        await using var db = database.Open();
        (await db.StockTransactions.SumAsync(x => x.TotalValue)).Should().Be(26);
        (await db.StockLayers.SumAsync(x => x.RemainingQty)).Should().Be(11);
        (await db.Accounts.SingleAsync(x => x.Code == "1131")).Balance.Should().Be(26);
        (await db.Accounts.SingleAsync(x => x.Code == "3200")).Balance.Should().Be(-26);
        (await db.JournalLines.SumAsync(x => x.Debit)).Should().Be(26);
        (await db.JournalLines.SumAsync(x => x.Credit)).Should().Be(26);
        var replay = await Send(provider, Command(preview.Data.PreviewHash, true));
        replay.Succeeded.Should().BeFalse();
        (await db.StockTransactions.CountAsync()).Should().Be(2);
    }

    [RawMaterialLocalDbFact]
    public async Task RealSqlServer_ConcurrentSameFile_CommitsOnlyOneImport()
    {
        await using var database = new RawMaterialImportTestDatabase();
        await database.Initialize();
        var services = new ServiceCollection(); database.Configure(services);
        await using var provider = services.BuildServiceProvider();
        var preview = await Send(provider, Command());
        var results = await Task.WhenAll(Send(provider, Command(preview.Data.PreviewHash, true)),
            Send(provider, Command(preview.Data.PreviewHash, true)));
        results.Count(x => x.Succeeded).Should().Be(1);
        await using var db = database.Open();
        (await db.Categories.CountAsync()).Should().Be(1);
        (await db.Products.CountAsync()).Should().Be(2);
        (await db.StockTransactions.CountAsync()).Should().Be(2);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }
}
