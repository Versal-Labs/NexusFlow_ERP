using Microsoft.EntityFrameworkCore;
using NexusFlow.Domain.Entities.Finance;

namespace NexusFlow.AppCore.Features.Migration;

public sealed class MigrationPageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? Module { get; set; }
    public string? Kind { get; set; }
    public string? Status { get; set; }
    public int Size => Math.Clamp(PageSize, 1, 100);
    public int Skip => (Math.Clamp(Page, 1, 1000000) - 1) * Size;
}
public sealed record MigrationPage<T>(List<T> Items, int Total, int Page, int PageSize);
public sealed partial class MigrationCentreService
{
    public async Task<object> OperationContextAsync(int id, int operationId, CancellationToken ct)
    {
        await OperationInProject(id, operationId, ct);
        var json = await db.MigrationOperationPayloads.AsNoTracking().Where(x => x.OperationId == operationId).Select(x => x.RequestJson).SingleAsync(ct);
        return MigrationJson.Read<MigrationOperationRequest>(json);
    }
    private static async Task<MigrationPage<T>> PageAsync<T>(IQueryable<T> query, MigrationPageQuery page, CancellationToken ct) =>
        new(await query.Skip(page.Skip).Take(page.Size).ToListAsync(ct), await query.CountAsync(ct), Math.Max(1, page.Page), page.Size);

    public async Task<object> WorkspaceAsync(int id, CancellationToken ct)
    {
        var p = await Project(id, false, ct);
        return new { Project = new { p.Id, p.Name, p.SourceSystem, p.OpeningDate, p.ClosingDate, p.Status, p.Version, p.ApprovedHash, p.FinalizedAt }, Settings = MigrationJson.Read<MigrationSettings>(p.SettingsJson),
            Datasets = await db.MigrationDatasets.AsNoTracking().Where(x => x.ProjectId == id).OrderBy(x => x.Module).ThenByDescending(x => x.Revision).Select(x => new { x.Id, x.Module, x.Revision, x.IsCurrent, x.FileName, x.ClosingDate }).ToListAsync(ct) };
    }
    public async Task<object> SummaryAsync(int id, CancellationToken ct)
    {
        var p = await db.MigrationProjects.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.Name, x.SourceSystem, x.OpeningDate, x.ClosingDate, x.Status, x.Version, x.ApprovedHash, x.FinalizedAt,
            AllDataDeclared = !x.SettingsJson.Contains(":\"AwaitingData\"") }).SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("Migration project not found.");
        var scopes = await db.MigrationScopes.AsNoTracking().Where(x => x.ProjectId == id).GroupBy(x => new { x.Kind, x.Status }).Select(g => new { g.Key.Kind, g.Key.Status, Count = g.Count() }).ToListAsync(ct);
        var units = await db.MigrationUnits.AsNoTracking().Where(x => x.ProjectId == id && x.Status == "Posted").GroupBy(x => x.Module).Select(g => new { Module = g.Key, Count = g.Count() }).ToListAsync(ct);
        var currentDatasets = await db.MigrationDatasets.CountAsync(x => x.ProjectId == id && x.IsCurrent, ct);
        var active = await db.MigrationOperations.AsNoTracking().Where(x => x.ProjectId == id && (x.Status == "Queued" || x.Status == "Running")).SingleOrDefaultAsync(ct);
        var latest = await db.MigrationOperations.AsNoTracking().Where(x => x.ProjectId == id).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var validation = await db.MigrationOperations.AsNoTracking().Where(x => x.ProjectId == id && x.Kind == "Validate" && (x.Status == "Completed" || x.Status == "CompletedWithIssues")).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var ledger = await db.MigrationOperations.AsNoTracking().Where(x => x.ProjectId == id && x.Kind == "LedgerCheck" && (x.Status == "Completed" || x.Status == "CompletedWithIssues")).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var legacy = await db.MigrationRuns.AsNoTracking().Where(x => x.ProjectId == id && (x.Status == "Queued" || x.Status == "Running") && !db.MigrationOperations.Any(o => o.RunId == x.Id)).Select(x => new { x.Id, x.Status, x.CommittedUnits }).FirstOrDefaultAsync(ct);
        var pending = scopes.Where(x => x.Status == "Pending").Sum(x => x.Count); var released = scopes.Where(x => x.Status is "Released" or "NoLegacyOpening").Sum(x => x.Count);
        var mapped = await db.MigrationAudits.AnyAsync(x => x.ProjectId == id && x.Action == "Mappings", ct);
        var finalized = p.Status == "Finalized";
        var stages = new[] {
            new { Number = 1, Name = "Upload files", Status = finalized || p.AllDataDeclared ? "Complete" : currentDatasets > 0 ? "In progress" : "Not started" },
            new { Number = 2, Name = "Map data", Status = finalized || mapped ? "Complete" : "Not started" },
            new { Number = 3, Name = "Validate and review", Status = finalized ? "Complete" : validation?.InputVersion != p.Version ? "Not started" : validation.NeedsAttention > 0 ? "Needs attention" : "Complete" },
            new { Number = 4, Name = "Approve and import", Status = finalized ? "Complete" : units.Count > 0 || p.ApprovedHash != null ? "In progress" : "Not started" },
            new { Number = 5, Name = "Reconcile and release", Status = finalized || released > 0 && pending == 0 ? "Complete" : pending > 0 || released > 0 ? "In progress" : "Not started" },
            new { Number = 6, Name = "Trial Balance and ledger", Status = finalized ? "Complete" : ledger?.InputVersion != p.Version ? "Not started" : ledger.NeedsAttention > 0 ? "Needs attention" : "Complete" },
            new { Number = 7, Name = "Final sign-off", Status = finalized ? "Complete" : latest?.Kind == "Finalize" && latest.Status == "Failed" ? "Needs attention" : "Not started" }
        };
        var activeStage = active?.Kind switch { "Validate" => 3, "Approve" or "Import" => 4, "Release" => 5, "LedgerCheck" => 6, "Finalize" => 7, _ => 0 };
        var failedStage = latest?.Status == "Failed" ? latest.Kind switch { "Validate" => 3, "Approve" or "Import" => 4, "Release" => 5, "LedgerCheck" => 6, "Finalize" => 7, _ => 0 } : 0;
        var nextStage = finalized ? 7 : activeStage > 0 ? activeStage : failedStage > 0 ? failedStage : currentDatasets == 0 ? 1 : !mapped ? 2 : pending > 0 && units.Any(x => MigrationCatalog.FinancialModules.Contains(x.Module)) ? 5 : pending == 0 && released > 0 && !units.Any(x => x.Module == "TrialBalance") ? 6 : validation?.InputVersion != p.Version ? 3 : validation.NeedsAttention > 0 ? 3 : validation.CanApprove ? 4 : pending > 0 ? 5 : ledger?.InputVersion != p.Version ? 6 : 7;
        return new { Project = p, CurrentDatasets = currentDatasets, PostedUnits = units.Sum(x => x.Count), Modules = units, Scopes = scopes, PendingScopes = pending, ReleasedScopes = released, ActiveOperation = active, LatestOperation = latest, LegacyRun = legacy, Validation = validation, Ledger = ledger, Stages = stages.Select(x => new { x.Number, x.Name, Status = activeStage == x.Number ? "In progress" : failedStage == x.Number ? "Needs attention" : x.Status }), RecommendedStage = nextStage, RecommendedAction = finalized ? "Review finalized evidence" : activeStage > 0 ? "Follow the current server operation" : failedStage > 0 ? "Review the stopped operation and its recovery message" : "Continue: " + stages[nextStage - 1].Name, ControlTotalsReady = units.Any(x => x.Module == "ControlTotals") };
    }
    public async Task<object> OperationProgressAsync(int id, int operationId, DateTime? after, CancellationToken ct)
    {
        var op = await OperationInProject(id, operationId, ct);
        var activityQuery = db.MigrationOperationItems.AsNoTracking().Where(x => x.OperationId == operationId && x.CompletedAt != null);
        if (after != null) activityQuery = activityQuery.Where(x => x.CompletedAt >= after);
        var activity = await activityQuery.OrderByDescending(x => x.CompletedAt).ThenByDescending(x => x.Id).Take(20).ToListAsync(ct);
        var state = op.JobId == null ? null : jobs.LookupState(op.JobId);
        return new { Operation = op, Activity = activity, ActivityCursor = activity.Count > 0 ? activity.Max(x => x.CompletedAt) : after, JobState = state?.Status.ToString(), CanResume = op.Status is "Queued" or "Failed" or "CompletedWithIssues" or "Running" && (op.JobId == null || state?.ConfirmedInactive == true) };
    }
    private async Task<MigrationOperation> OperationInProject(int id, int operationId, CancellationToken ct) => await db.MigrationOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operationId && x.ProjectId == id, ct) ?? throw new InvalidOperationException("Operation not found in this project.");
    public Task<MigrationPage<MigrationScope>> ScopesPageAsync(int id, MigrationPageQuery page, CancellationToken ct)
    {
        var q = db.MigrationScopes.AsNoTracking().Where(x => x.ProjectId == id);
        if (!string.IsNullOrWhiteSpace(page.Kind)) q = q.Where(x => x.Kind == page.Kind);
        if (!string.IsNullOrWhiteSpace(page.Status)) q = q.Where(x => x.Status == page.Status);
        if (!string.IsNullOrWhiteSpace(page.Search)) q = q.Where(x => x.Code.Contains(page.Search));
        return PageAsync(q.OrderBy(x => x.Code).ThenBy(x => x.Id), page, ct);
    }
    public Task<MigrationPage<MigrationOperationItem>> OperationItemsPageAsync(int id, int operationId, MigrationPageQuery page, CancellationToken ct)
    {
        var q = db.MigrationOperationItems.AsNoTracking().Where(x => x.OperationId == operationId && db.MigrationOperations.Any(o => o.Id == x.OperationId && o.ProjectId == id));
        if (!string.IsNullOrWhiteSpace(page.Status)) q = q.Where(x => x.Status == page.Status);
        if (!string.IsNullOrWhiteSpace(page.Module)) q = q.Where(x => x.Module == page.Module);
        if (!string.IsNullOrWhiteSpace(page.Kind)) q = q.Where(x => x.Kind == page.Kind);
        if (!string.IsNullOrWhiteSpace(page.Search)) q = q.Where(x => x.Code.Contains(page.Search) || x.Message != null && x.Message.Contains(page.Search));
        return PageAsync(q.OrderBy(x => x.Ordinal), page, ct);
    }
    public async Task<object> OperationItemDetailAsync(int id, int operationId, int itemId, MigrationPageQuery page, CancellationToken ct)
    {
        var operation = await OperationInProject(id, operationId, ct);
        var item = await db.MigrationOperationItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == itemId && x.OperationId == operationId, ct) ?? throw new InvalidOperationException("Item not found.");
        var json = await db.MigrationOperationItemPayloads.AsNoTracking().Where(x => x.ItemId == itemId).Select(x => x.DetailJson).SingleOrDefaultAsync(ct);
        if (item.Kind == "Account") return new { Item = item, Control = json == null ? null : MigrationJson.Read<MigrationControl>(json) };
        MigrationPreviewUnit? u = json == null ? null : MigrationJson.Read<MigrationPreviewUnit>(json);
        if (u == null && operation.RunId is { } runId)
        {
            var run = await db.MigrationRuns.AsNoTracking().SingleAsync(x => x.Id == runId && x.ProjectId == id, ct);
            u = (await ReadRunPayloadAsync(run, ct)).Preview.Units.SingleOrDefault(x => x.Module == item.Module && x.Scope == item.Code);
        }
        if (u == null) return new { Item = item };
        var rows = u.Rows.AsEnumerable(); if (!string.IsNullOrWhiteSpace(page.Search)) rows = rows.Where(x => x.Data.Values.Any(v => v.Contains(page.Search, StringComparison.OrdinalIgnoreCase)));
        var list = rows.ToList();
        return new { Item = item, Journal = u.Journal.Skip(page.Skip).Take(page.Size), JournalCount = u.Journal.Count, Errors = u.Errors.Skip(page.Skip).Take(page.Size), ErrorCount = u.Errors.Count, Warnings = u.Warnings.Skip(page.Skip).Take(page.Size), WarningCount = u.Warnings.Count, Rows = new MigrationPage<MigrationRow>(list.Skip(page.Skip).Take(page.Size).ToList(), list.Count, Math.Max(1, page.Page), page.Size) };
    }
    public async Task<object> IssuesPageAsync(int id, int operationId, MigrationPageQuery page, bool grouped, CancellationToken ct)
    {
        await OperationInProject(id, operationId, ct);
        var q = db.MigrationOperationIssues.AsNoTracking().Where(x => x.OperationId == operationId);
        if (!string.IsNullOrWhiteSpace(page.Status)) q = q.Where(x => x.Severity == page.Status);
        if (!string.IsNullOrWhiteSpace(page.Module)) q = q.Where(x => x.Module == page.Module);
        if (!string.IsNullOrWhiteSpace(page.Search)) q = q.Where(x => x.Message.Contains(page.Search) || x.Code.Contains(page.Search));
        if (grouped) return await PageAsync(q.GroupBy(x => new { x.Severity, x.Module, x.Message }).Select(g => new { g.Key.Severity, g.Key.Module, g.Key.Message, Count = g.Count(), AffectedRows = g.Sum(x => x.AffectedRows) }).OrderByDescending(x => x.Count).ThenBy(x => x.Message), page, ct);
        return await PageAsync(q.OrderBy(x => x.Id), page, ct);
    }
    public async Task<object> HistoryPageAsync(int id, string list, MigrationPageQuery page, CancellationToken ct)
    {
        if (list == "operations")
        {
            var q = db.MigrationOperations.AsNoTracking().Where(x => x.ProjectId == id);
            if (!string.IsNullOrWhiteSpace(page.Kind)) q = q.Where(x => x.Kind == page.Kind);
            if (!string.IsNullOrWhiteSpace(page.Status)) q = q.Where(x => x.Status == page.Status);
            return await PageAsync(q.OrderByDescending(x => x.Id), page, ct);
        }
        if (list == "runs")
        {
            var q = db.MigrationRuns.AsNoTracking().Where(x => x.ProjectId == id);
            if (!string.IsNullOrWhiteSpace(page.Status)) q = q.Where(x => x.Status == page.Status);
            if (!string.IsNullOrWhiteSpace(page.Search)) q = q.Where(x => x.Id.ToString().Contains(page.Search) || x.Error != null && x.Error.Contains(page.Search));
            return await PageAsync(q.OrderByDescending(x => x.Id).Select(x => new { x.Id, x.Status, x.CommittedUnits, x.Error, x.JobId, x.CreatedAt, x.CompletedAt, OperationId = db.MigrationOperations.Where(o => o.RunId == x.Id).Select(o => (int?)o.Id).FirstOrDefault() }), page, ct);
        }
        if (list == "audit")
        {
            var q = db.MigrationAudits.AsNoTracking().Where(x => x.ProjectId == id);
            if (!string.IsNullOrWhiteSpace(page.Search)) q = q.Where(x => x.Action.Contains(page.Search) || x.Detail.Contains(page.Search));
            return await PageAsync(q.OrderByDescending(x => x.Id), page, ct);
        }
        if (list == "units")
        {
            var q = db.MigrationUnits.AsNoTracking().Where(x => x.ProjectId == id);
            if (!string.IsNullOrWhiteSpace(page.Search)) q = q.Where(x => x.ScopeKey.Contains(page.Search));
            if (!string.IsNullOrWhiteSpace(page.Module)) q = q.Where(x => x.Module == page.Module);
            if (!string.IsNullOrWhiteSpace(page.Status)) q = q.Where(x => x.Status == page.Status);
            return await PageAsync(q.OrderByDescending(x => x.Id).Select(x => new { x.Id, x.Module, x.ScopeKey, x.Status, x.JournalEntryId }), page, ct);
        }
        if (list == "mappings")
        {
            var settings = MigrationJson.Read<MigrationSettings>((await Project(id, false, ct)).SettingsJson);
            var maps = settings.Mappings.SelectMany(x => x.Value.Select(m => new { Kind = x.Key, Code = m.Key, TargetId = m.Value }));
            if (!string.IsNullOrWhiteSpace(page.Kind)) maps = maps.Where(x => x.Kind == page.Kind);
            if (!string.IsNullOrWhiteSpace(page.Search)) maps = maps.Where(x => x.Code.Contains(page.Search, StringComparison.OrdinalIgnoreCase));
            var rows = maps.OrderBy(x => x.Kind).ThenBy(x => x.Code).ToList(); return new { Items = rows.Skip(page.Skip).Take(page.Size), Total = rows.Count, Page = Math.Max(1, page.Page), PageSize = page.Size };
        }
        throw new InvalidOperationException("Unknown migration list.");
    }
}
