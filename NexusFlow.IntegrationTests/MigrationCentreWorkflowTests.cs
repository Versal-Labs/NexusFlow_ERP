using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Features.Finance.CustomerMigration;
using NexusFlow.AppCore.Features.Migration;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.IntegrationTests;

public sealed partial class MigrationCentreTests
{
    [CustomerMigrationSqlFact]
    public async Task PreloadedFinancialJournal_CannotReceiveASecondOpeningPosition()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        var result = await scope.ServiceProvider.GetRequiredService<IJournalService>().PostJournalAsync(new() { Date = Opening, Module = "Inventory", ReferenceNo = "PRELOADED", Lines = [new() { AccountId = await db.Accounts.Where(x => x.Code == "AR").Select(x => x.Id).SingleAsync(), Debit = 100 }, new() { AccountId = await db.Accounts.Where(x => x.Code == "OBE").Select(x => x.Id).SingleAsync(), Credit = 100 }] }); result.Succeeded.Should().BeTrue();
        Func<Task> create = () => scope.ServiceProvider.GetRequiredService<MigrationCentreService>().CreateAsync(new("Must not duplicate", "OLD", Opening), default); await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already contains financial journals*"); (await db.MigrationProjects.CountAsync()).Should().Be(0);
    }
    [CustomerMigrationSqlFact]
    public async Task ColumnProfile_CreatesAndReconcilesTheActualPartyScope()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "CustomerOpenings");
        await Upload(provider, id, "CustomerOpenings", "old_code,old_ref,old_date,open_amt,type\nC001,PROFILE-INV,2026-09-01,125,Invoice\n");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); var settings = MigrationJson.Read<MigrationSettings>((await db.MigrationProjects.SingleAsync()).SettingsJson);
            settings.Columns["CustomerOpenings"] = new() { ["Code"] = "old_code", ["Reference"] = "old_ref", ["DocumentDate"] = "old_date", ["Amount"] = "open_amt", ["Kind"] = "type" };
            var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>(); await service.SaveSettingsAsync(id, settings, default);
            (await db.MigrationScopes.SingleAsync()).Code.Should().Be("C001");
            Func<Task> skip = () => service.SaveScopeAsync(id, new("Customer", "C001", ["CustomerOpenings", "ReceivedCheques"], "False zero", true), default); await skip.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Uploaded legacy*");
        }
        await Post(provider, id, new() { Modules = ["CustomerOpenings"] }); await Release(provider, id, "Customer", "C001");
        using var check = provider.CreateScope(); (await check.ServiceProvider.GetRequiredService<IErpDbContext>().CustomerOpeningItems.SingleAsync()).Amount.Should().Be(125);
    }
    [CustomerMigrationSqlFact]
    public async Task ConcurrentExecutionClicks_AndDuplicateDelivery_DoNotCreateExtraJournals()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "CustomerOpenings");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,A,Invoice,2026-09-01,100\n"); var selection = new MigrationSelection { Modules = ["CustomerOpenings"] };
        using (var scope = provider.CreateScope()) { var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>(); var p = await service.ValidateAsync(id, selection, default); await service.ApproveAsync(id, new(selection, p.Hash, "Concurrency fixture"), default); }
        async Task<int?> Execute() { using var scope = provider.CreateScope(); try { return await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ExecuteAsync(id, selection, default); } catch (InvalidOperationException) { return null; } }
        var clicks = await Task.WhenAll(Execute(), Execute()); clicks.Count(x => x != null).Should().Be(1); var run = clicks.Single(x => x != null)!.Value;
        for (var i = 0; i < 2; i++) { using var scope = provider.CreateScope(); await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ExecuteRunAsync(run, default); }
        using var check = provider.CreateScope(); var db = check.ServiceProvider.GetRequiredService<IErpDbContext>(); (await db.MigrationRuns.CountAsync()).Should().Be(1); (await db.JournalEntries.CountAsync()).Should().Be(1); (await db.CustomerOpeningItems.CountAsync()).Should().Be(1);
    }
    [CustomerMigrationSqlFact]
    public async Task CompatibleLegacyCustomerBatch_IsAdoptedWithoutPostingAgain()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database);
        using (var scope = provider.CreateScope())
        {
            var old = scope.ServiceProvider.GetRequiredService<CustomerMigrationService>(); var files = CustomerMigrationFixtures.Files("30/09/2026");
            var review = await old.CreateAsync(files.Outstanding, "cusout.xls", files.Cheques, "cheques.xlsx", Opening, Closing, default); review.CanPost.Should().BeTrue(); await old.PostAsync(review.BatchId, review.ReviewHash, true, default);
        }
        using var check = provider.CreateScope(); var db = check.ServiceProvider.GetRequiredService<IErpDbContext>(); var before = await db.JournalEntries.CountAsync(); var service = check.ServiceProvider.GetRequiredService<MigrationCentreService>();
        var project = await service.CreateAsync(new("Adopt compatible closing", "OLD-SYSTEM", Opening, "Verified original cutover batch and retained control evidence"), default);
        var preview = await service.ValidateAsync(project.Id, new() { Modules = ["CustomerOpenings", "ReceivedCheques"] }, default);
        preview.Units.SelectMany(x => x.Errors).Should().BeEmpty(); preview.Units.Should().OnlyContain(x => x.Status == "Already imported"); (await db.JournalEntries.CountAsync()).Should().Be(before); (await db.MigrationUnits.CountAsync(x => x.Module == "LegacyCustomerBatch")).Should().Be(1);
    }
}
