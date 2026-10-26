using System.Data.Common;
using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Features.Migration;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Infrastructure;

namespace NexusFlow.IntegrationTests;

public sealed partial class MigrationCentreTests
{
    private static async Task<MigrationOperation> Start(ServiceProvider provider, int id, string kind, MigrationSelection? selection = null, string hash = "", MigrationReleaseSelection? release = null, Guid? requestId = null)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().StartOperationAsync(id, new() { Kind = kind, ClientRequestId = requestId ?? Guid.NewGuid(), Selection = selection ?? new(), Hash = hash, Evidence = "Synthetic independent signed evidence", Release = release ?? new() }, default);
    }
    private static async Task<MigrationOperation> Work(ServiceProvider provider, int operationId, CancellationToken ct = default)
    {
        using (var scope = provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().RunOperationAsync(operationId, ct);
        using var check = provider.CreateScope(); return await check.ServiceProvider.GetRequiredService<IErpDbContext>().MigrationOperations.AsNoTracking().SingleAsync(x => x.Id == operationId);
    }
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [CustomerMigrationSqlFact]
    public async Task DurableOperations_ValidateApproveImport_WithIdentityAndImmutablePayloads()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize();
        var actor = new OperationTestUser();
        using var provider = Provider(database, services => services.AddSingleton<ICurrentUserService>(actor));
        var id = await Create(provider, "CustomerOpenings");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,A,Invoice,2026-09-01,100\nB,C002,B,Invoice,2026-09-01,200\n");
        var selection = new MigrationSelection { Modules = ["CustomerOpenings"], Customers = ["C001"] };
        var validationRequest = await Start(provider, id, "Validate", selection); actor.UserId = "SYSTEM";
        var validation = await Work(provider, validationRequest.Id); actor.UserId = "initiating-finance-user";
        validation.Status.Should().Be("Completed"); validation.CanApprove.Should().BeTrue(); validation.Total.Should().Be(1); validation.Processed.Should().Be(1);
        var approvalRequest = await Start(provider, id, "Approve", selection, validation.ResultHash!); actor.UserId = "SYSTEM";
        var approval = await Work(provider, approvalRequest.Id); approval.Status.Should().Be("Completed"); actor.UserId = "initiating-finance-user";
        var importRequest = await Start(provider, id, "Import", selection); actor.UserId = "SYSTEM";
        var import = await Work(provider, importRequest.Id);
        import.Status.Should().Be("Completed"); import.Succeeded.Should().Be(1); import.Total.Should().Be(1);
        await Work(provider, import.Id); // Duplicate delivery cannot change completed work.
        using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await db.MigrationProjects.SingleAsync()).ApprovedBy.Should().Be("initiating-finance-user");
        (await db.MigrationAudits.Where(x => x.Action == "Approved").SingleAsync()).CreatedBy.Should().Be("initiating-finance-user");
        (await db.JournalEntries.SingleAsync()).CreatedBy.Should().Be("initiating-finance-user");
        (await db.MigrationRuns.SingleAsync()).SelectionJson.Should().Be("{}"); (await db.MigrationRunPayloads.CountAsync()).Should().Be(1);
        (await db.CustomerOpeningItems.CountAsync()).Should().Be(1);
        var importedItem = await db.MigrationOperationItems.SingleAsync(x => x.OperationId == import.Id);
        var importedDetail = Json(await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().OperationItemDetailAsync(id, import.Id, importedItem.Id, new(), default));
        importedDetail.GetProperty("rows").GetProperty("total").GetInt32().Should().Be(1); importedDetail.GetProperty("journalCount").GetInt32().Should().Be(2);
        // Failure counts the selected run's units, excluding a different customer's later run.
        await Post(provider, id, new() { Modules = ["CustomerOpenings"], Customers = ["C002"] });
        var run = await db.MigrationRuns.SingleAsync(x => x.Id == import.RunId); run.Status = "Running"; await db.SaveChangesAsync(default);
        await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().MarkRunFailedAsync(run.Id, "Synthetic stop", default);
        run.CommittedUnits.Should().Be(1);
    }

    [CustomerMigrationSqlFact]
    public async Task BackgroundLedgerAndFinalization_SaveAccountResults_AndFinalizedCompletion()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database);
        var id = await Create(provider, "CustomerOpenings", "TrialBalance");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,A,Invoice,2026-09-01,100\n");
        await Post(provider, id, new() { Modules = ["CustomerOpenings"] }); await Release(provider, id, "Customer", "C001");
        await Upload(provider, id, "TrialBalance", "Code,Debit,Credit\nAR,100,0\nSAFE,0,0\nBANK,0,0\nOBE,0,100\nREV,0,0\nAP,0,0\n");
        await Post(provider, id, new() { Modules = ["TrialBalance"] });
        var ledger = await Work(provider, (await Start(provider, id, "LedgerCheck")).Id);
        ledger.Status.Should().Be("Completed"); ledger.Total.Should().Be(6); ledger.Processed.Should().Be(6); ledger.NeedsAttention.Should().Be(0);
        var finalized = await Work(provider, (await Start(provider, id, "Finalize")).Id);
        finalized.Status.Should().Be("Completed"); finalized.Succeeded.Should().Be(1);
        await Work(provider, finalized.Id);
        using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await db.MigrationProjects.SingleAsync()).Status.Should().Be("Finalized"); (await db.MigrationOperations.SingleAsync(x => x.Id == finalized.Id)).Status.Should().Be("Completed");
        (await db.MigrationOperationItemPayloads.CountAsync()).Should().Be(6);
        Func<Task> reopen = () => Start(provider, id, "Import", new() { Modules = ["TrialBalance"] }); await reopen.Should().ThrowAsync<InvalidOperationException>().WithMessage("*finalized and read-only*");
    }

    [CustomerMigrationSqlFact]
    public async Task RequestIds_ConcurrentClicks_AndLegacyGuards_AreShared()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database);
        var id = await Create(provider, "CustomerOpenings"); await Upload(provider, id, "CustomerOpenings", "Code,Reference,Kind,DocumentDate,Amount\nC001,A,Invoice,2026-09-01,100\n");
        var requestId = Guid.NewGuid(); var selection = new MigrationSelection { Modules = ["CustomerOpenings"] };
        var clicks = await Task.WhenAll(Start(provider, id, "Validate", selection, requestId: requestId), Start(provider, id, "Validate", selection, requestId: requestId));
        clicks[0].Id.Should().Be(clicks[1].Id);
        Func<Task> competing = () => Start(provider, id, "Validate", selection); await competing.Should().ThrowAsync<InvalidOperationException>().WithMessage("*operation is active*");
        using var scope = provider.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>(); var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        var settings = MigrationJson.Read<MigrationSettings>((await db.MigrationProjects.SingleAsync()).SettingsJson);
        Func<Task> edit = () => service.SaveSettingsAsync(id, settings, default); await edit.Should().ThrowAsync<InvalidOperationException>().WithMessage("*operation is active*");
        Func<Task> legacy = () => service.ExecuteAsync(id, selection, default); await legacy.Should().ThrowAsync<InvalidOperationException>().WithMessage("*operation is active*");
        (await db.MigrationOperations.CountAsync()).Should().Be(1); await Work(provider, clicks[0].Id);
    }

    [CustomerMigrationSqlFact]
    public async Task EnqueueOutage_RetainsAcceptedOperation_AndDispatcherCanRecover()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); var jobs = new OperationTestJobs { EnqueueUnavailable = true };
        using var provider = Provider(database, services => services.AddSingleton<IBackgroundJobService>(jobs)); var id = await Create(provider);
        var op = await Start(provider, id, "Validate", new() { Modules = ["Accounts"] }); op.Status.Should().Be("Queued"); op.JobId.Should().BeNull();
        jobs.EnqueueUnavailable = false;
        using (var scope = provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().DispatchOperationAsync(op.Id, false, default);
        using (var scope = provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().DispatchOperationAsync(op.Id, false, default);
        jobs.SuccessfulEnqueues.Should().Be(1); (await Work(provider, op.Id)).Status.Should().Be("Completed");
    }

    [CustomerMigrationSqlFact]
    public async Task Resume_RequiresConfirmedInactiveJob_AndReleasedWorkerLock_AndUnchangedInputs()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); var jobs = new OperationTestJobs();
        using var provider = Provider(database, services => services.AddSingleton<IBackgroundJobService>(jobs)); var id = await Create(provider);
        var op = await Start(provider, id, "Validate", new() { Modules = ["Accounts"] });
        using (var read = provider.CreateScope())
            Json(await read.ServiceProvider.GetRequiredService<MigrationCentreService>().OperationProgressAsync(id, op.Id, null, default)).GetProperty("canResume").GetBoolean().Should().BeTrue("a confirmed stopped job must remain recoverable even if it never claimed its queued record");
        using (var change = provider.CreateScope()) { var db = change.ServiceProvider.GetRequiredService<IErpDbContext>(); var saved = await db.MigrationOperations.SingleAsync(); saved.Status = "Running"; await db.SaveChangesAsync(default); }
        async Task Resume() { using var scope = provider.CreateScope(); await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ResumeOperationAsync(id, op.Id, default); }
        foreach (var state in new[] { BackgroundJobStatus.Unavailable, BackgroundJobStatus.Active }) { jobs.State = state; Func<Task> unknown = Resume; await unknown.Should().ThrowAsync<InvalidOperationException>().WithMessage("*inactivity is confirmed*"); }
        jobs.State = BackgroundJobStatus.Missing;
        using (var lockScope = provider.CreateScope())
        using (await lockScope.ServiceProvider.GetRequiredService<IErpDbContext>().AcquireMigrationWorkerAsync(id))
        { Func<Task> live = Resume; await live.Should().ThrowAsync<InvalidOperationException>().WithMessage("*worker is still active*"); }
        var enqueueLeaseAvailable = false;
        jobs.BeforeEnqueue = () =>
        {
            using var proofScope = provider.CreateScope();
            using var proof = proofScope.ServiceProvider.GetRequiredService<IErpDbContext>().AcquireMigrationWorkerAsync(id).GetAwaiter().GetResult();
            enqueueLeaseAvailable = true;
        };
        await Resume(); jobs.BeforeEnqueue = null; enqueueLeaseAvailable.Should().BeTrue("a newly enqueued worker must not be blocked by Resume's own lease");
        (await Work(provider, op.Id)).Status.Should().Be("Completed");
        using (var lockScope = provider.CreateScope())
        using (await lockScope.ServiceProvider.GetRequiredService<IErpDbContext>().AcquireMigrationWorkerAsync(id))
        {
            using var editScope = provider.CreateScope(); var editDb = editScope.ServiceProvider.GetRequiredService<IErpDbContext>();
            var settings = MigrationJson.Read<MigrationSettings>((await editDb.MigrationProjects.SingleAsync()).SettingsJson);
            Func<Task> legacyWorkerEdit = () => editScope.ServiceProvider.GetRequiredService<MigrationCentreService>().SaveSettingsAsync(id, settings, default);
            await legacyWorkerEdit.Should().ThrowAsync<InvalidOperationException>().WithMessage("*worker is still active*");
        }
        var next = await Start(provider, id, "Validate", new() { Modules = ["Accounts"] });
        using (var change = provider.CreateScope()) { var db = change.ServiceProvider.GetRequiredService<IErpDbContext>(); var saved = await db.MigrationOperations.SingleAsync(x => x.Id == next.Id); saved.Status = "Failed"; await db.SaveChangesAsync(default); }
        await Upload(provider, id, "Accounts", "Code,Name,AccountType\nNEW,New Account,Asset\n");
        using var attempt = provider.CreateScope(); Func<Task> changed = () => attempt.ServiceProvider.GetRequiredService<MigrationCentreService>().ResumeOperationAsync(id, next.Id, default);
        await changed.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Inputs changed*");
    }

    [CustomerMigrationSqlFact]
    public async Task Release_ReportsSelectedTotals_ContinuesBusinessFailures_AndPreservesChecks()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database);
        var id = await Create(provider, "CustomerOpenings", "ControlTotals");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,A,Invoice,2026-09-01,100\nB,C002,B,Invoice,2026-09-01,200\n");
        // Second customer's independent amount deliberately differs.
        await Upload(provider, id, "ControlTotals", "Kind,Code,Component,Amount,RowCount\nCustomer,C001,CustomerOpenings:Debit,100,1\nCustomer,C001,CustomerOpenings:Credit,0,1\nCustomer,C001,ReceivedCheques,0,0\nCustomer,C002,CustomerOpenings:Debit,199,1\nCustomer,C002,CustomerOpenings:Credit,0,1\nCustomer,C002,ReceivedCheques,0,0\n");
        await Post(provider, id, new() { Modules = ["CustomerOpenings", "ControlTotals"] });
        var op = await Work(provider, (await Start(provider, id, "Release", release: new() { Kind = "Customer", AllMatching = true, ExpectedCount = 2 })).Id);
        op.Status.Should().Be("CompletedWithIssues"); op.Total.Should().Be(2); op.Processed.Should().Be(2); op.Succeeded.Should().Be(1); op.NeedsAttention.Should().Be(1);
        using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await db.MigrationScopes.SingleAsync(x => x.Code == "C001")).Status.Should().Be("Released"); (await db.MigrationScopes.SingleAsync(x => x.Code == "C002")).Status.Should().Be("Pending");
        (await db.MigrationOperationItems.CountAsync(x => x.Status == "Succeeded")).Should().Be(1); (await db.MigrationAudits.CountAsync(x => x.Action == "Released")).Should().Be(1);
        var result = Json(await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().SummaryAsync(id, default)); result.GetProperty("pendingScopes").GetInt32().Should().Be(1);
    }

    [CustomerMigrationSqlFact]
    public async Task Release2050_InterruptedAfter223_ReopensAndResumes_WithoutDuplicateRelease()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize();
        using var cancellation = new CancellationTokenSource(); var stop = new StopReleaseAfterCommit(223, cancellation);
        using var provider = Provider(database, services => services.AddDbContext<ErpDbContext>(options => options.AddInterceptors(stop)));
        var id = await Create(provider, "ControlTotals");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); var sample = await db.Customers.FirstAsync();
            for (var i = 3; i <= 2050; i++) db.Customers.Add(new() { LegacyCustomerCode = "R" + i.ToString("D5"), Name = "Synthetic customer " + i, CustomerGroupId = sample.CustomerGroupId, PriceLevelId = sample.PriceLevelId, PaymentTermId = sample.PaymentTermId, DefaultReceivableAccountId = sample.DefaultReceivableAccountId, DefaultRevenueAccountId = sample.DefaultRevenueAccountId });
            await db.SaveChangesAsync(default);
            var customers = await db.Customers.Select(x => new { x.Id, Code = x.LegacyCustomerCode! }).ToListAsync();
            foreach (var c in customers) db.MigrationScopes.Add(new() { ProjectId = id, Kind = "Customer", Code = c.Code, EntityId = c.Id, RequiredModulesJson = MigrationJson.Write(new[] { "CustomerOpenings", "ReceivedCheques" }) });
            await db.SaveChangesAsync(default);
        }
        List<string> codes;
        using (var scope = provider.CreateScope()) codes = await scope.ServiceProvider.GetRequiredService<IErpDbContext>().MigrationScopes.OrderBy(x => x.Id).Select(x => x.Code).ToListAsync();
        await Upload(provider, id, "ControlTotals", "Kind,Code,Component,Amount,RowCount\n" + string.Join("\n", codes.SelectMany(code => new[] { $"Customer,{code},CustomerOpenings:Debit,0,0", $"Customer,{code},CustomerOpenings:Credit,0,0", $"Customer,{code},ReceivedCheques,0,0" })) + "\n");
        // Persist a synthetic already-imported independent schedule, including its origin records.
        // This avoids making 6,150 individual fixture posts while exercising all release checks.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); var preview = await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ValidateAsync(id, new() { Modules = ["ControlTotals"] }, default);
            preview.Errors.Should().BeEmpty(); preview.Units.Single().Errors.Should().BeEmpty(); var u = preview.Units.Single();
            var unit = new MigrationUnit { ProjectId = id, Module = u.Module, ScopeKey = u.Scope, Hash = u.Hash, RowsJson = MigrationJson.Write(u.Rows) }; db.MigrationUnits.Add(unit); await db.SaveChangesAsync(default);
            foreach (var row in u.Rows) db.MigrationOrigins.Add(new() { ProjectId = id, UnitId = unit.Id, SourceSystem = "TEST-LEGACY", RecordKind = u.Module, IdentityHash = MigrationJson.Hash(MigrationCatalog.Identity(u.Module, row)), ContentHash = MigrationJson.Hash(row.Canonical), LegacyRecordId = MigrationCatalog.Identity(u.Module, row), EntityKind = "MigrationUnit", EntityId = unit.Id });
            await db.SaveChangesAsync(default);
        }
        var operation = await Start(provider, id, "Release", release: new() { Kind = "Customer", AllMatching = true, ExpectedCount = 2050 });
        Func<Task> interrupted = () => Work(provider, operation.Id, cancellation.Token); await interrupted.Should().ThrowAsync<OperationCanceledException>();
        using (var reopened = provider.CreateScope())
        {
            var db = reopened.ServiceProvider.GetRequiredService<IErpDbContext>(); var service = reopened.ServiceProvider.GetRequiredService<MigrationCentreService>(); var saved = await db.MigrationOperations.SingleAsync();
            saved.Status.Should().Be("Failed"); saved.Total.Should().Be(2050); saved.Succeeded.Should().Be(223); (await db.MigrationScopes.CountAsync(x => x.Status == "Released")).Should().Be(223);
            var summary = Json(await service.SummaryAsync(id, default)); summary.GetProperty("pendingScopes").GetInt32().Should().Be(1827);
            var page = await service.ScopesPageAsync(id, new() { Kind = "Customer", Status = "Pending" }, default); page.Items.Should().HaveCount(25); page.Total.Should().Be(1827);
            await service.ResumeOperationAsync(id, operation.Id, default);
        }
        var completed = await Work(provider, operation.Id); completed.Status.Should().Be("Completed"); completed.Processed.Should().Be(2050); completed.Succeeded.Should().Be(2050); completed.NeedsAttention.Should().Be(0);
        await Work(provider, operation.Id);
        using var final = provider.CreateScope(); var finalDb = final.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await finalDb.MigrationScopes.CountAsync(x => x.Status == "Released")).Should().Be(2050); (await finalDb.MigrationAudits.CountAsync(x => x.Action == "Released")).Should().Be(2050); (await finalDb.JournalEntries.CountAsync()).Should().Be(0);
    }

    [CustomerMigrationSqlFact]
    public async Task Paging_GroupedWarnings_AndPolling_AreBoundedAndReadOnly()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database);
        var id = await Create(provider, "CustomerOpenings");
        var rows = string.Join("\n", Enumerable.Range(1, 125).Select(i => $"C001,REF{i},Invoice,2026-09-01,1"));
        await Upload(provider, id, "CustomerOpenings", "Code,Reference,Kind,DocumentDate,Amount\n" + rows + "\n");
        var op = await Work(provider, (await Start(provider, id, "Validate", new() { Modules = ["CustomerOpenings"] })).Id);
        using var scope = provider.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>(); var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        var issuePage = Json(await service.IssuesPageAsync(id, op.Id, new() { Status = "Warning" }, true, default)); issuePage.GetProperty("items")[0].GetProperty("affectedRows").GetInt32().Should().Be(125);
        var item = await db.MigrationOperationItems.SingleAsync(); var detail = Json(await service.OperationItemDetailAsync(id, op.Id, item.Id, new() { PageSize = 500 }, default)); detail.GetProperty("rows").GetProperty("items").GetArrayLength().Should().Be(100);
        var payload = await db.MigrationOperationItemPayloads.Select(x => x.DetailJson).SingleAsync(); var auditCount = await db.MigrationAudits.CountAsync(); var update = (await db.MigrationOperations.SingleAsync()).UpdatedAt;
        for (var i = 0; i < 4; i++) { await service.SummaryAsync(id, default); await service.OperationProgressAsync(id, op.Id, null, default); }
        (await db.MigrationAudits.CountAsync()).Should().Be(auditCount); (await db.MigrationOperations.AsNoTracking().SingleAsync()).UpdatedAt.Should().Be(update); (await db.MigrationOperationItemPayloads.Select(x => x.DetailJson).SingleAsync()).Should().Be(payload);
        await using var context = database.Open();
        foreach (var type in new[] { typeof(MigrationOperation), typeof(MigrationOperationItem), typeof(MigrationOperationPayload), typeof(MigrationOperationItemPayload), typeof(MigrationOperationIssue), typeof(MigrationRunData) }) context.Model.FindEntityType(type)!.IsTemporal().Should().BeFalse();
    }
}

internal sealed class OperationTestUser : ICurrentUserService
{
    public string UserId { get; set; } = "initiating-finance-user";
    public string UserName => "Synthetic finance approver";
    public bool IsAuthenticated => true;
    public int? EmployeeId => null;
    public bool HasPermission(string permission) => true;
}
internal sealed class OperationTestJobs : IBackgroundJobService
{
    public bool EnqueueUnavailable { get; set; }
    public int SuccessfulEnqueues { get; private set; }
    public BackgroundJobStatus State { get; set; } = BackgroundJobStatus.Stopped;
    public Action? BeforeEnqueue { get; set; }
    public string EnqueueAsync<T>(Expression<Func<T, Task>> call) { if (EnqueueUnavailable) throw new IOException("Synthetic job outage"); BeforeEnqueue?.Invoke(); SuccessfulEnqueues++; return Guid.NewGuid().ToString("N"); }
    public BackgroundJobState LookupState(string id) => new(State);
    public string Enqueue<T>(Expression<Action<T>> call) => throw new NotSupportedException();
    public string Schedule<T>(Expression<Action<T>> call, TimeSpan delay) => throw new NotSupportedException();
    public void AddOrUpdateRecurring<T>(string id, Expression<Action<T>> call, string cron, string queue = "default") => throw new NotSupportedException();
    public void RemoveRecurring(string id) => throw new NotSupportedException();
}
internal sealed class StopReleaseAfterCommit(int count, CancellationTokenSource cancellation) : DbTransactionInterceptor
{
    private bool stopped;
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (!stopped && eventData.Context?.ChangeTracker.Entries<MigrationOperation>().Any(x => x.Entity.Kind == "Release" && x.Entity.Succeeded == count) == true)
        { stopped = true; cancellation.Cancel(); }
        return Task.CompletedTask;
    }
}

