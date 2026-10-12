using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Features.Migration;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Services;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Enums;
using NexusFlow.Infrastructure.Services;

namespace NexusFlow.IntegrationTests;

public sealed partial class MigrationCentreTests
{
    private static readonly DateTime Opening = new(2026, 10, 1);
    private static readonly DateTime Closing = Opening.AddDays(-1);
    private static byte[] Csv(string text) => Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n"));
    private static ServiceProvider Provider(CustomerMigrationTestDatabase database, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection(); database.Configure(services);
        services.AddScoped<IMigrationFileReader, MigrationFileReader>(); services.AddSingleton<IGlobalStorageCoordinator, MemoryMigrationStorage>(); services.AddSingleton<IBackgroundJobService, MigrationTestJobs>();
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    private static async Task<int> Create(ServiceProvider provider, params string[] nonzero)
    {
        using var scope = provider.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>();
        var p = await service.CreateAsync(new("Synthetic migration", "TEST-LEGACY", Opening), default);
        var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); var settings = new MigrationSettings { OpeningEquityAccountId = await db.Accounts.Where(x => x.Code == "OBE").Select(x => x.Id).SingleAsync() };
        foreach (var m in MigrationCatalog.Modules.Keys) if (!nonzero.Contains(m)) { settings.DatasetStates[m] = "NotApplicable"; settings.ZeroEvidence[m] = "Synthetic signed zero schedule"; }
        settings.Defaults = new() { ["SafeAccount"] = await db.Accounts.Where(x => x.Code == "SAFE").Select(x => x.Id).SingleAsync() };
        await service.SaveSettingsAsync(p.Id, settings, default); return p.Id;
    }
    private static async Task Upload(ServiceProvider provider, int id, string module, string text)
    {
        using var scope = provider.CreateScope(); await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().UploadAsync(id, module, module + ".csv", Csv(text), Closing, default);
    }
    private static async Task<int> Post(ServiceProvider provider, int id, MigrationSelection selection)
    {
        int run;
        using (var scope = provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>(); var preview = await service.ValidateAsync(id, selection, default);
            preview.Errors.Should().BeEmpty(); preview.Units.SelectMany(x => x.Errors).Should().BeEmpty(); preview.CanApprove.Should().BeTrue();
            await service.ApproveAsync(id, new(selection, preview.Hash, "Synthetic complete opening and physical verification"), default); run = await service.ExecuteAsync(id, selection, default);
        }
        using (var scope = provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ExecuteRunAsync(run, default);
        return run;
    }
    private static async Task Release(ServiceProvider provider, int id, string kind, string code)
    {
        using var scope = provider.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>();
        var controls = await service.ReconcileScopeAsync(id, kind, code, default);
        await service.ReleaseAsync(id, kind, code, new("Synthetic independently verified controls", controls.ToDictionary(x => x.Key, x => x.Source), controls.ToDictionary(x => x.Key, x => x.Rows)), default);
    }
    [Fact]
    public void CanonicalReader_RejectsFormulas_PreservesCodesAndQuotedValues()
    {
        var reader = new MigrationFileReader();
        var result = reader.Read("CustomerMaster", "customers.csv", Csv("Code,Name,Phone\n000012,\"A, B\",001947\n"));
        result.Rows.Single().Get("Code").Should().Be("000012"); result.Rows.Single().Get("Name").Should().Be("A, B"); result.Rows.Single().Get("Phone").Should().Be("001947");
        var files = CustomerMigrationFixtures.Files(formula: true); Action read = () => reader.Read("CustomerOpenings", "outstanding.xlsx", files.Outstanding); read.Should().Throw<InvalidDataException>();
        Action broken = () => reader.Read("CustomerMaster", "bad.csv", Csv("Code,Name\nC1,\"unclosed")); broken.Should().Throw<InvalidDataException>();
        Action corrupt = () => reader.Read("Stock", "bad.xlsx", [80]); corrupt.Should().Throw<InvalidDataException>();
    }
    [Fact]
    public void TemplatePack_IsVersioned_ContainsEveryDatasetAndNoBusinessData()
    {
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(MigrationCentreService.Templates()));
        zip.Entries.Should().HaveCount(MigrationCatalog.Modules.Count + 2);
        foreach (var module in MigrationCatalog.Modules.Keys) { using var r = new StreamReader(zip.GetEntry(module + ".csv")!.Open()); r.ReadToEnd().Trim().Split('\n').Should().HaveCount(1); }
    }
    [CustomerMigrationSqlFact]
    public async Task SharedControlAccount_LaterCustomerAndReorderedFiles_AreExactlyOnce()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database);
        var id = await Create(provider, "CustomerOpenings");
        const string first = "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,INV-A,Invoice,2026-09-01,1000\nB,C002,INV-B,Invoice,2026-09-02,500\n";
        await Upload(provider, id, "CustomerOpenings", first);
        await Post(provider, id, new() { Modules = ["CustomerOpenings"], Customers = ["C001"] }); await Release(provider, id, "Customer", "C001");
        await Post(provider, id, new() { Modules = ["CustomerOpenings"], Customers = ["C002"] }); await Release(provider, id, "Customer", "C002");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nB,C002,INV-B,Invoice,2026-09-02,500\nA,C001,INV-A,Invoice,2026-09-01,1000\n");
        using var scope = provider.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>(); var preview = await service.ValidateAsync(id, new() { Modules = ["CustomerOpenings"] }, default);
        preview.Units.SelectMany(x => x.Errors).Should().BeEmpty(); preview.Units.Should().OnlyContain(x => x.Status == "Already imported"); preview.CanApprove.Should().BeFalse();
        var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); (await db.Accounts.SingleAsync(x => x.Code == "AR")).Balance.Should().Be(1500); (await db.CustomerOpeningItems.CountAsync()).Should().Be(2); (await db.MigrationOrigins.CountAsync()).Should().Be(2);
    }
    [CustomerMigrationSqlFact]
    public async Task ChangedSnapshotAndStaleApproval_CannotPost()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "CustomerOpenings");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,INV-A,Invoice,2026-09-01,1000\n");
        using var scope = provider.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>(); var selection = new MigrationSelection { Modules = ["CustomerOpenings"] }; var preview = await service.ValidateAsync(id, selection, default); await service.ApproveAsync(id, new(selection, preview.Hash, "Test approval"), default);
        await service.UploadAsync(id, "CustomerOpenings", "changed.csv", Csv("LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,INV-A,Invoice,2026-09-01,900\n"), Closing, default);
        Func<Task> stale = () => service.ExecuteAsync(id, selection, default); await stale.Should().ThrowAsync<InvalidOperationException>();
        Func<Task> date = () => service.UploadAsync(id, "CustomerOpenings", "later.csv", Csv("Code,Amount\nC001,100\n"), Closing.AddDays(1), default); await date.Should().ThrowAsync<InvalidOperationException>();
        var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); (await db.CustomerOpeningItems.CountAsync()).Should().Be(0); (await db.JournalEntries.CountAsync()).Should().Be(0);
    }
    [CustomerMigrationSqlFact]
    public async Task SelectedValidParty_IsNotBlockedByUnrelatedMissingMaster_AndPendingTradingIsBlocked()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "CustomerOpenings", "ReceivedCheques");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,INV-A,Invoice,2026-09-01,1000\nX,MISSING,INV-X,Invoice,2026-09-01,250\n");
        await Post(provider, id, new() { Modules = ["CustomerOpenings"], Customers = ["C001"] });
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); var customer = await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C001");
            db.PaymentTransactions.Add(new() { CustomerId = customer.Id, ReferenceNo = "BLOCKED", Date = Opening, Amount = 1 }); Func<Task> write = () => db.SaveChangesAsync(default); await write.Should().ThrowAsync<InvalidOperationException>().WithMessage("*locked*");
        }
        Func<Task> release = () => Release(provider, id, "Customer", "C001"); await release.Should().ThrowAsync<InvalidOperationException>().WithMessage("*awaiting data*");
        using (var scope = provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>(); var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); var project = await db.MigrationProjects.SingleAsync(); var settings = MigrationJson.Read<MigrationSettings>(project.SettingsJson); settings.DatasetStates["ReceivedCheques"] = "NotApplicable"; settings.ZeroEvidence["ReceivedCheques"] = "Independent no-cheque declaration"; await service.SaveSettingsAsync(id, settings, default);
        }
        await Release(provider, id, "Customer", "C001");
    }
    [CustomerMigrationSqlFact]
    public async Task NegativeCustomerOpening_IsUsableCredit_AndPreviewedJournalBalances()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "CustomerOpenings");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,INV-A,Invoice,2026-09-01,1000\nR,C001,RETURN-A,ReturnCredit,2026-09-02,-150\n"); await Post(provider, id, new() { Modules = ["CustomerOpenings"] }); await Release(provider, id, "Customer", "C001");
        using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); var customer = await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C001"); var balance = await scope.ServiceProvider.GetRequiredService<CustomerBalanceService>().GetAsync(customer.Id, Opening); balance.NetBalance.Should().Be(850); balance.OpeningCredits.Single().Available.Should().Be(150); balance.Debits.Single().DueDate.Should().Be(new DateTime(2026, 10, 1));
        (await db.JournalLines.SumAsync(x => x.Debit)).Should().Be(await db.JournalLines.SumAsync(x => x.Credit));
    }
    [CustomerMigrationSqlFact]
    public async Task ResidualLedger_ExcludesLaterTrading_AndFinalizationLocksProject()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "CustomerOpenings", "TrialBalance");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,INV-A,Invoice,2026-09-01,1000\n"); await Post(provider, id, new() { Modules = ["CustomerOpenings"] }); await Release(provider, id, "Customer", "C001");
        // New trading uses normal journals after this party is released; it must not change the migration residual.
        using (var scope = provider.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); var ar = await db.Accounts.SingleAsync(x => x.Code == "AR"); var rev = await db.Accounts.SingleAsync(x => x.Code == "REV"); var result = await scope.ServiceProvider.GetRequiredService<IJournalService>().PostJournalAsync(new() { Date = Opening.AddDays(1), Module = "Sales", ReferenceNo = "NEW-SALE", Lines = [new() { AccountId = ar.Id, Debit = 25 }, new() { AccountId = rev.Id, Credit = 25 }] }); result.Succeeded.Should().BeTrue(); }
        await Upload(provider, id, "TrialBalance", "Code,Debit,Credit\nAR,1000,0\nSAFE,0,0\nBANK,0,0\nOBE,0,800\nREV,0,200\nAP,0,0\n"); await Post(provider, id, new() { Modules = ["TrialBalance"] });
        using var scope2 = provider.CreateScope(); var service = scope2.ServiceProvider.GetRequiredService<MigrationCentreService>(); (await service.ReconcileLedgerAsync(id, default)).Should().OnlyContain(x => x.Difference == 0); await service.FinalizeAsync(id, "Synthetic CFO sign-off", default);
        Func<Task> upload = () => service.UploadAsync(id, "CustomerOpenings", "new.csv", Csv("Code,Amount\nC001,1\n"), Closing, default); await upload.Should().ThrowAsync<InvalidOperationException>().WithMessage("*read-only*");
        var context = scope2.ServiceProvider.GetRequiredService<IErpDbContext>(); (await context.Accounts.SingleAsync(x => x.Code == "AR")).Balance.Should().Be(1025); (await context.Accounts.SingleAsync(x => x.Code == "REV")).Balance.Should().Be(-225);
        using (var later = provider.CreateScope())
        {
            var db = later.ServiceProvider.GetRequiredService<IErpDbContext>(); var customer = await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C002");
            db.PaymentTransactions.Add(new() { CustomerId = customer.Id, ReferenceNo = "AFTER-FINAL", Date = Opening.AddDays(2), Amount = 1 });
            await db.SaveChangesAsync(default); // Newly trading masters no longer require migration scope classification.
        }
        using (var backdated = provider.CreateScope())
        {
            var db = backdated.ServiceProvider.GetRequiredService<IErpDbContext>(); var customer = await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C002");
            db.PaymentTransactions.Add(new() { CustomerId = customer.Id, ReferenceNo = "BEFORE-GO-LIVE", Date = Opening.AddDays(-1), Amount = 1 });
            Func<Task> write = () => db.SaveChangesAsync(default); await write.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot predate go-live*");
        }
    }
}
internal sealed class MemoryMigrationStorage : IGlobalStorageCoordinator
{
    private readonly Dictionary<string, byte[]> files = new();
    public async Task<string> SaveFileSecurelyAsync(Stream stream, string name, string folder, string contentType, CancellationToken ct = default) { using var memory = new MemoryStream(); await stream.CopyToAsync(memory, ct); var key = Guid.NewGuid().ToString("N"); lock (files) files[key] = memory.ToArray(); return key; }
    public Task<(Stream Stream, string ContentType)> RetrieveFileAsync(string reference, CancellationToken ct = default) { lock (files) return Task.FromResult(((Stream)new MemoryStream(files[reference]), "application/octet-stream")); }
    public Task DeleteFileAsync(string reference, CancellationToken ct = default) { lock (files) files.Remove(reference); return Task.CompletedTask; }
}
internal sealed class MigrationTestJobs : IBackgroundJobService
{
    public string Enqueue<T>(Expression<Action<T>> call) => Guid.NewGuid().ToString("N");
    public string EnqueueAsync<T>(Expression<Func<T, Task>> call) => Guid.NewGuid().ToString("N");
    public string? GetState(string id) => "Succeeded";
    public string Schedule<T>(Expression<Action<T>> call, TimeSpan delay) => "test";
    public void AddOrUpdateRecurring<T>(string id, Expression<Action<T>> call, string cron, string queue = "default") { }
    public void RemoveRecurring(string id) { }
}
