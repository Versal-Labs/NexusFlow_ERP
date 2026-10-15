using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Features.Migration;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.IntegrationTests;

public sealed partial class MigrationCentreTests
{
    [CustomerMigrationSqlFact]
    public async Task ScopedBankReconciliation_StillRejectsReferencesInUnselectedChequeFiles()
    {
        await using var database = new CustomerMigrationTestDatabase();
        await database.Initialize();
        using var provider = Provider(database);
        var id = await Create(provider, "BankRecon", "ReceivedCheques", "IssuedCheques");
        await Upload(provider, id, "ReceivedCheques", "Code,Reference,BankAccountCode\nC001,RECEIVED-REF,BANK\n");
        await Upload(provider, id, "IssuedCheques", "Code,Reference,BankAccountCode\nS001,ISSUED-REF,BANK\n");
        await Upload(provider, id, "BankRecon", "LegacyRecordId,Code,Reference,DocumentDate,Amount,Kind,BankReflected\nR,BANK,RECEIVED-REF,2026-09-01,10,OutstandingDeposit,true\nI,BANK,ISSUED-REF,2026-09-01,-10,UnclearedPayment,true\n");

        using var scope = provider.CreateScope();
        var preview = await scope.ServiceProvider.GetRequiredService<MigrationCentreService>()
            .ValidateAsync(id, new() { Modules = ["BankRecon"], Banks = ["BANK"] }, default);
        preview.Errors.Should().BeEmpty();
        preview.Units.Should().ContainSingle();
        preview.Units[0].Errors.Count(x => x.Message.Contains("already appears in a cheque dataset")).Should().Be(2);
        (await scope.ServiceProvider.GetRequiredService<IErpDbContext>().JournalEntries.CountAsync()).Should().Be(0);
    }

    [CustomerMigrationSqlFact]
    public async Task AtomicGroupValidation_PreservesNewMasterDependenciesAndApprovedAmounts()
    {
        await using var database = new CustomerMigrationTestDatabase();
        await database.Initialize();
        using var provider = Provider(database);
        var id = await Create(provider, "CustomerMaster", "CustomerOpenings");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
            var settings = MigrationJson.Read<MigrationSettings>((await db.MigrationProjects.SingleAsync()).SettingsJson);
            settings.Defaults["ReceivableAccount"] = await db.Accounts.Where(x => x.Code == "AR").Select(x => x.Id).SingleAsync();
            settings.Defaults["RevenueAccount"] = await db.Accounts.Where(x => x.Code == "REV").Select(x => x.Id).SingleAsync();
            settings.Defaults["CustomerGroup"] = await db.SystemLookups.Where(x => x.Type == "CustomerGroup").Select(x => x.Id).SingleAsync();
            settings.Defaults["PriceLevel"] = await db.SystemLookups.Where(x => x.Type == "PriceLevel").Select(x => x.Id).SingleAsync();
            settings.Defaults["CustomerPaymentTerm"] = await db.SystemLookups.Where(x => x.Type == "PaymentTerm").Select(x => x.Id).SingleAsync();
            await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().SaveSettingsAsync(id, settings, default);
        }
        await Upload(provider, id, "CustomerMaster", "Code,Name\n00003,New customer three\n00004,New customer four\n");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,00003,A,Invoice,2026-09-01,100\nB,00004,B,ReturnCredit,2026-09-01,-25\n");
        await Post(provider, id, new() { Modules = ["CustomerMaster", "CustomerOpenings"] });

        using var check = provider.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await context.CustomerOpeningItems.OrderBy(x => x.Amount).Select(x => x.Amount).ToListAsync()).Should().Equal(-25m, 100m);
        (await context.Customers.Where(x => x.LegacyCustomerCode == "00003" || x.LegacyCustomerCode == "00004").CountAsync()).Should().Be(2);
        (await context.Accounts.SingleAsync(x => x.Code == "AR")).Balance.Should().Be(75m);
        (await context.MigrationUnits.CountAsync()).Should().Be(4);
        var service = check.ServiceProvider.GetRequiredService<MigrationCentreService>();
        var whole = await service.ValidateAsync(id, new() { Modules = ["CustomerOpenings"] }, default);
        var selected = await service.ValidateAsync(id, new() { Modules = ["CustomerOpenings"], Customers = ["00003"] }, default);
        selected.Units.Single().Hash.Should().Be(whole.Units.Single(x => x.Scope == "00003").Hash);
        selected.Units.Single().Status.Should().Be("Already imported");
        selected.Units.Single().Errors.Should().BeEmpty();
    }

    [CustomerMigrationSqlFact]
    public async Task BulkRelease_ContinuesAfterPartialCompletion_WithoutPostingOrReleasingTwice()
    {
        await using var database = new CustomerMigrationTestDatabase();
        await database.Initialize();
        using var provider = Provider(database);
        var id = await Create(provider, "CustomerOpenings", "ControlTotals");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,A,Invoice,2026-09-01,100\nB,C002,B,Invoice,2026-09-01,200\n");
        await Upload(provider, id, "ControlTotals", "Kind,Code,Component,Amount,RowCount\nCustomer,C001,CustomerOpenings:Debit,100,1\nCustomer,C001,CustomerOpenings:Credit,0,1\nCustomer,C001,ReceivedCheques,0,0\nCustomer,C002,CustomerOpenings:Debit,200,1\nCustomer,C002,CustomerOpenings:Credit,0,1\nCustomer,C002,ReceivedCheques,0,0\n");
        await Post(provider, id, new() { Modules = ["CustomerOpenings", "ControlTotals"] });
        await Release(provider, id, "Customer", "C001");
        var request = new MigrationBulkReleaseRequest(new() { Modules = ["CustomerMaster"] }, "Independent signed controls; continue interrupted release");
        using (var scope = provider.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ReleaseSelectedAsync(id, request, default);
            result.Released.Should().Be(1);
            result.Pending.Should().BeEmpty();
        }
        using (var scope = provider.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ReleaseSelectedAsync(id, request, default);
            result.Released.Should().Be(0);
            result.Pending.Should().BeEmpty();
        }
        using var check = provider.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await db.MigrationScopes.CountAsync(x => x.Status == "Released")).Should().Be(2);
        (await db.MigrationAudits.CountAsync(x => x.Action == "Released")).Should().Be(2);
        (await db.JournalEntries.CountAsync()).Should().Be(2);
        (await db.CustomerOpeningItems.CountAsync()).Should().Be(2);
        (await db.Accounts.SingleAsync(x => x.Code == "AR")).Balance.Should().Be(300m);
    }
}