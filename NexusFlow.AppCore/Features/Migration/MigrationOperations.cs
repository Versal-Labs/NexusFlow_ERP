using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;

namespace NexusFlow.AppCore.Features.Migration;

public sealed class MigrationOperationRequest
{
    public string Kind { get; set; } = "";
    public Guid ClientRequestId { get; set; }
    public MigrationSelection Selection { get; set; } = new();
    public string Hash { get; set; } = "";
    public string Evidence { get; set; } = "";
    public MigrationReleaseSelection Release { get; set; } = new();
}
public sealed class MigrationReleaseSelection
{
    public string Kind { get; set; } = "Customer";
    public string Search { get; set; } = "";
    public bool AllMatching { get; set; }
    public List<int> ScopeIds { get; set; } = [];
    public int? ExpectedCount { get; set; }
}
public sealed partial class MigrationCentreService
{
    private int? owningOperationId;
    private int? owningRunId;
    private bool owningLegacyRelease;
    private string? operationActor;
    private string ActorId => operationActor ?? user.UserId;
    private sealed class OwnershipLease(Action release) : IDisposable { public void Dispose() => release(); }
    private IDisposable OwnRun(int id)
    {
        var previous = owningRunId; owningRunId = id;
        return new OwnershipLease(() => owningRunId = previous);
    }
    private static bool Active(string status) => status is "Queued" or "Running";

    private async Task<string> InputFingerprint(int id, CancellationToken ct, int? operationId = null)
    {
        var p = await db.MigrationProjects.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var datasets = await db.MigrationDatasets.AsNoTracking().Where(x => x.ProjectId == id && x.IsCurrent)
            .OrderBy(x => x.Module).Select(x => new { x.Id, x.Module, x.FileHash, x.ClosingDate }).ToListAsync(ct);
        var scopes = await db.MigrationScopes.AsNoTracking().Where(x => x.ProjectId == id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Kind, x.Code, x.RequiredModulesJson }).ToListAsync(ct);
        var units = await db.MigrationUnits.AsNoTracking().Where(x => x.ProjectId == id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Module, x.ScopeKey, x.Hash, x.Status, x.JournalEntryId, x.ReversalJournalEntryId }).ToListAsync(ct);
        var version = p.Version;
        if (operationId is { } ownerId)
        {
            var owner = await db.MigrationOperations.AsNoTracking().SingleAsync(x => x.Id == ownerId && x.ProjectId == id, ct);
            // Each release increments Version with its successful checkpoint. Account for exactly
            // those increments; another user's scope/settings change must still invalidate Resume.
            if (owner.Kind == "Release") version -= owner.Succeeded;
            if (owner.Kind == "Import" && owner.RunId is { } runId)
            {
                var run = await db.MigrationRuns.AsNoTracking().SingleAsync(x => x.Id == runId, ct);
                var ownTargets = (await ReadRunPayloadAsync(run, ct)).Preview.Units.Where(x => x.Status == "Pending").Select(x => (x.Module, x.Scope, x.Hash)).ToHashSet();
                units = units.Where(x => x.Status != "Posted" || !ownTargets.Contains((x.Module, x.ScopeKey, x.Hash))).ToList();
            }
        }
        return MigrationJson.Hash(MigrationJson.Write(new { p.SourceSystem, p.OpeningDate, p.ClosingDate, p.SettingsJson, Version = version, datasets, scopes, units }));
    }
    public async Task<MigrationOperation> StartOperationAsync(int id, MigrationOperationRequest request, CancellationToken ct)
    {
        if (request.ClientRequestId == Guid.Empty) throw new InvalidOperationException("A client request ID is required.");
        if (request.Kind is not ("Validate" or "Approve" or "Import" or "Release" or "LedgerCheck" or "Finalize")) throw new InvalidOperationException("Choose a supported migration operation.");
        MigrationOperation operation;
        using (var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            await db.LockMigrationProjectAsync(id, ct);
            var repeated = await db.MigrationOperations.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == id && x.ClientRequestId == request.ClientRequestId, ct);
            if (repeated != null) return repeated;
            var p = await Project(id, request.Kind is not ("Validate" or "LedgerCheck"), ct); await Editable(p, ct);
            using var noLegacyWorker = await db.AcquireMigrationWorkerAsync(id, ct);
            if (request.Kind is "Approve" or "Release" or "Finalize" && string.IsNullOrWhiteSpace(request.Evidence)) throw new InvalidOperationException("Record the required evidence beside this action.");
            if (request.Kind is "Validate" or "Approve" or "Import" && (request.Selection.Modules.Count == 0 || request.Selection.Modules.Except(MigrationCatalog.Modules.Keys).Any())) throw new InvalidOperationException("Select known import modules.");
            operation = new() { ProjectId = id, Kind = request.Kind, ClientRequestId = request.ClientRequestId, InitiatedBy = user.UserId, InitiatedByName = user.UserName, InputVersion = p.Version, InputFingerprint = await InputFingerprint(id, ct) };
            List<MigrationScope> targets = [];
            if (request.Kind == "Release")
            {
                if (request.Release.Kind is not ("Customer" or "Supplier" or "Stock" or "Bank")) throw new InvalidOperationException("Choose Customer, Supplier, Stock or Bank.");
                var query = db.MigrationScopes.AsNoTracking().Where(x => x.ProjectId == id && x.Kind == request.Release.Kind && x.Status == "Pending");
                var search = request.Release.Search.Trim();
                if (request.Release.AllMatching) { if (search != "") query = query.Where(x => x.Code.Contains(search)); }
                else query = query.Where(x => request.Release.ScopeIds.Contains(x.Id));
                targets = await query.OrderBy(x => x.Id).ToListAsync(ct);
                if (targets.Count == 0) throw new InvalidOperationException("Choose pending scopes to release.");
                if (!request.Release.AllMatching && targets.Count != request.Release.ScopeIds.Distinct().Count() || request.Release.ExpectedCount is { } expected && targets.Count != expected) throw new InvalidOperationException("Pending targets changed. Refresh the scope list and review the exact count.");
                operation.Total = targets.Count;
            }
            db.MigrationOperations.Add(operation); await db.SaveChangesAsync(ct);
            db.MigrationOperationPayloads.Add(new() { OperationId = operation.Id, RequestJson = MigrationJson.Write(request) });
            for (var i = 0; i < targets.Count; i++) db.MigrationOperationItems.Add(new() { OperationId = operation.Id, Ordinal = i, Kind = targets[i].Kind, Code = targets[i].Code, ScopeId = targets[i].Id });
            Audit(id, "Operation queued", $"{request.Kind} operation {operation.Id}; initiated by {operation.InitiatedBy}; {targets.Count} frozen release targets.");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        // Acceptance is durable even when the request disconnects or enqueue is unavailable.
        await DispatchOperationAsync(operation.Id, false, CancellationToken.None);
        return operation;
    }
    public async Task DispatchOperationAsync(int operationId, bool resume, CancellationToken ct)
    {
        var projectId = await db.MigrationOperations.AsNoTracking().Where(x => x.Id == operationId).Select(x => x.ProjectId).SingleAsync(ct);
        IDisposable? worker = null;
        try
        {
            if (resume) worker = await db.AcquireMigrationWorkerAsync(projectId, ct);
            using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await db.LockMigrationProjectAsync(projectId, ct);
            var op = await db.MigrationOperations.SingleAsync(x => x.Id == operationId, ct);
            if (op.Status == "Completed") return;
            if (!resume && (op.Status != "Queued" || op.JobId != null)) return;
            if (op.JobId != null && !jobs.LookupState(op.JobId).ConfirmedInactive) throw new InvalidOperationException("The job is active or the job service cannot confirm it has stopped. Resume is unavailable until inactivity is confirmed.");
            if (resume)
            {
                if (op.Status == "CompletedWithIssues" && op.Kind is "Validate" or "LedgerCheck") throw new InvalidOperationException("Start a fresh validation or full ledger check to review these results again.");
                if (await db.MigrationOperations.AnyAsync(x => x.ProjectId == projectId && x.Id != operationId && (x.Status == "Queued" || x.Status == "Running"), ct) || await db.MigrationRuns.AnyAsync(x => x.ProjectId == projectId && x.Id != op.RunId && (x.Status == "Queued" || x.Status == "Running"), ct)) throw new InvalidOperationException("Another migration operation or posting run is active.");
                await Project(projectId, op.Kind is not ("Validate" or "LedgerCheck"), ct);
                if (op.InputFingerprint != await InputFingerprint(projectId, ct, op.Id)) throw new InvalidOperationException("Inputs changed. Start fresh validation and approval where required; this operation cannot resume.");
                var unresolved = await db.MigrationOperationItems.Where(x => x.OperationId == op.Id && x.Status == "NeedsAttention").ToListAsync(ct);
                foreach (var item in unresolved) { item.Status = "Pending"; item.Message = null; item.CompletedAt = null; }
                op.NeedsAttention = 0; op.Processed = op.Succeeded;
                if (op.RunId is { } runId) { var run = await db.MigrationRuns.SingleAsync(x => x.Id == runId, ct); run.Status = "Queued"; run.Error = null; }
            }
            op.Status = "Queued"; op.CompletedAt = null; op.Error = null; op.Phase = "Waiting for a worker"; op.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            // Keep the project transaction locked, but release the inactivity proof before
            // enqueue: a fast worker (including RCSI readers) must not reject its own lease.
            worker?.Dispose(); worker = null;
            try { op.JobId = jobs.EnqueueAsync<MigrationOperationJob>(job => job.RunAsync(operationId)); }
            catch { op.JobId = null; op.Error = "Waiting for the background job service. Dispatch will be checked again within 30 seconds."; }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        finally { worker?.Dispose(); }
    }
    public async Task ResumeOperationAsync(int id, int operationId, CancellationToken ct)
    {
        if (!await db.MigrationOperations.AnyAsync(x => x.Id == operationId && x.ProjectId == id, ct)) throw new InvalidOperationException("Operation not found in this project.");
        await DispatchOperationAsync(operationId, true, ct);
    }

    public async Task RunOperationAsync(int operationId, CancellationToken ct)
    {
        var snapshot = await db.MigrationOperations.AsNoTracking().SingleAsync(x => x.Id == operationId, ct);
        // Duplicate delivery may occur after an uncertain enqueue. Only one session can work.
        IDisposable lease;
        try { lease = await db.AcquireMigrationWorkerAsync(snapshot.ProjectId, ct); }
        catch (InvalidOperationException) { return; }
        using (lease)
        {
            using (var claim = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            {
                await db.LockMigrationProjectAsync(snapshot.ProjectId, ct);
                var op = await db.MigrationOperations.SingleAsync(x => x.Id == operationId, ct);
                if (op.Status != "Queued") return;
                op.Status = "Running"; op.StartedAt ??= DateTime.UtcNow; op.UpdatedAt = DateTime.UtcNow; op.Phase = "Preparing " + op.Kind;
                await db.SaveChangesAsync(ct); await claim.CommitAsync(ct);
            }
            owningOperationId = operationId; operationActor = snapshot.InitiatedBy;
            using var actor = db.EnterMigrationActor(snapshot.InitiatedBy);
            try
            {
                if (snapshot.InputFingerprint != await InputFingerprint(snapshot.ProjectId, ct, snapshot.Id)) throw new InvalidOperationException("Inputs changed. Validate and approve the current data again.");
                var request = MigrationJson.Read<MigrationOperationRequest>(await db.MigrationOperationPayloads.Where(x => x.OperationId == operationId).Select(x => x.RequestJson).SingleAsync(ct));
                var op = await db.MigrationOperations.SingleAsync(x => x.Id == operationId, ct);
                switch (op.Kind)
                {
                    case "Validate":
                        await StoreValidationAsync(op, await ValidateAsync(op.ProjectId, request.Selection, ct, async (total, processed, current, issues) =>
                        {
                            op.Total = total; op.Processed = processed; op.Succeeded = processed - issues; op.NeedsAttention = issues;
                            op.Phase = "Validating source units"; op.CurrentItem = current; op.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
                        }), ct); break;
                    case "Approve": await ApproveAsync(op.ProjectId, new(request.Selection, request.Hash, request.Evidence), ct); op.Total = 1; op.Processed = op.Succeeded = 1; break;
                    case "Import":
                        var runId = op.RunId ?? await ExecuteAsync(op.ProjectId, request.Selection, ct);
                        owningRunId = runId; await ExecuteRunAsync(runId, ct);
                        var savedImport = await db.MigrationOperations.AsNoTracking().SingleAsync(x => x.Id == op.Id, ct);
                        op.Processed = savedImport.Processed; op.Succeeded = savedImport.Succeeded; op.NeedsAttention = savedImport.NeedsAttention; break;
                    case "Release": await RunReleaseOperationAsync(op, request, ct); break;
                    case "LedgerCheck":
                        op.Total = await db.Accounts.CountAsync(x => x.IsTransactionAccount && x.IsActive, ct); op.Phase = "Checking accounts"; await db.SaveChangesAsync(ct);
                        var previous = await db.MigrationOperationItems.Where(x => x.OperationId == op.Id && x.Status == "Succeeded").Select(x => x.Code).ToListAsync(ct);
                        await ReconcileLedgerAsync(op.ProjectId, ct, async control =>
                        {
                            if (previous.Contains(control.Key)) return;
                            using var checkpoint = await db.BeginTransactionAsync(ct);
                            var item = await db.MigrationOperationItems.SingleOrDefaultAsync(x => x.OperationId == op.Id && x.Code == control.Key, ct);
                            if (item == null) { item = new() { OperationId = op.Id, Ordinal = await db.MigrationOperationItems.CountAsync(x => x.OperationId == op.Id, ct), Kind = "Account", Code = control.Key }; db.MigrationOperationItems.Add(item); }
                            item.Status = control.Difference == 0 ? "Succeeded" : "NeedsAttention"; item.Message = control.Difference == 0 ? "Balanced" : "Ledger difference: " + control.Difference; item.CompletedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(ct);
                            var detail = await db.MigrationOperationItemPayloads.SingleOrDefaultAsync(x => x.ItemId == item.Id, ct);
                            if (detail == null) db.MigrationOperationItemPayloads.Add(new() { ItemId = item.Id, DetailJson = MigrationJson.Write(control) }); else detail.DetailJson = MigrationJson.Write(control);
                            op.Processed++; if (control.Difference == 0) op.Succeeded++; else op.NeedsAttention++;
                            op.CurrentItem = control.Key; op.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
                            await checkpoint.CommitAsync(ct);
                        }); break;
                    case "Finalize": await FinalizeAsync(op.ProjectId, request.Evidence, ct); op.Total = 1; op.Processed = op.Succeeded = 1; break;
                }
                op.Status = op.NeedsAttention > 0 ? "CompletedWithIssues" : "Completed"; op.Phase = "Finished"; op.CompletedAt = op.UpdatedAt = DateTime.UtcNow;
                Audit(op.ProjectId, "Operation completed", $"{op.Kind} {op.Id}: {op.Succeeded} successful; {op.NeedsAttention} needing attention; target {op.Total}."); await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Discard tracked state from failed/rolled-back financial transactions.
                using var clean = scopeFactory.CreateScope();
                var service = clean.ServiceProvider.GetRequiredService<MigrationCentreService>();
                await service.FailOperationAsync(operationId, ex.Message, CancellationToken.None);
                throw;
            }
        }
    }
    private async Task FailOperationAsync(int operationId, string message, CancellationToken ct)
    {
        var op = await db.MigrationOperations.SingleAsync(x => x.Id == operationId, ct);
        using var actor = db.EnterMigrationActor(op.InitiatedBy);
        if (!Active(op.Status)) return;
        op.Status = "Failed"; op.Error = message.Length > 4000 ? message[..4000] : message; op.Phase = "Stopped safely; committed work retained"; op.CompletedAt = op.UpdatedAt = DateTime.UtcNow;
        if (op.RunId is { } runId) await MarkRunFailedAsync(runId, message, ct);
        Audit(op.ProjectId, "Operation stopped", $"{op.Kind} {op.Id}: {op.Error}"); await db.SaveChangesAsync(ct);
    }
    private async Task CompleteSimpleOperationAsync(CancellationToken ct)
    {
        if (owningOperationId is not { } id) return;
        var op = await db.MigrationOperations.SingleAsync(x => x.Id == id, ct);
        op.Total = op.Processed = op.Succeeded = 1; op.Status = "Completed"; op.Phase = "Finished"; op.CompletedAt = op.UpdatedAt = DateTime.UtcNow;
    }
    private async Task StoreValidationAsync(MigrationOperation op, MigrationPreview preview, CancellationToken ct)
    {
        // Immutable preview units are written once; subsequent progress never serializes them.
        if (await db.MigrationOperationItems.AnyAsync(x => x.OperationId == op.Id, ct)) return;
        using var tx = await db.BeginTransactionAsync(ct);
        op.Processed = op.Succeeded = op.NeedsAttention = 0;
        op.ResultHash = preview.Hash; op.CanApprove = preview.CanApprove; op.Total = preview.Units.Count;
        foreach (var error in preview.Errors) AddIssue(op.Id, null, error, "Error");
        for (var i = 0; i < preview.Units.Count; i++)
        {
            var u = preview.Units[i]; var bad = u.Errors.Count > 0;
            var item = new MigrationOperationItem { OperationId = op.Id, Ordinal = i, Module = u.Module, Kind = MigrationCatalog.ScopeKind(u.Module), Code = u.Scope, Status = bad ? "NeedsAttention" : "Succeeded", Rows = u.Rows.Count, Message = u.Status, CompletedAt = DateTime.UtcNow };
            db.MigrationOperationItems.Add(item); await db.SaveChangesAsync(ct);
            db.MigrationOperationItemPayloads.Add(new() { ItemId = item.Id, DetailJson = MigrationJson.Write(u) });
            foreach (var error in u.Errors) AddIssue(op.Id, item.Id, error, "Error");
            foreach (var warning in u.Warnings)
            {
                var match = global::System.Text.RegularExpressions.Regex.Match(warning, @"^Row (\d+):\s*");
                var row = match.Success ? int.Parse(match.Groups[1].Value) : 0;
                var text = match.Success ? warning[match.Length..] : warning.StartsWith(u.Scope + ": ") ? warning[(u.Scope.Length + 2)..] : warning;
                AddIssue(op.Id, item.Id, new(u.Module, u.Scope, row, text), "Warning", row > 0 ? 1 : u.Rows.Count);
            }
            op.Processed++; if (bad) op.NeedsAttention++; else op.Succeeded++;
        }
        op.NeedsAttention += preview.Errors.Count; op.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private void AddIssue(int operationId, int? itemId, MigrationIssue issue, string severity, int affectedRows = 1) => db.MigrationOperationIssues.Add(new() { OperationId = operationId, ItemId = itemId, Severity = severity, Module = issue.Module, Code = issue.Scope, Row = issue.Row, AffectedRows = affectedRows, Message = issue.Message.Length > 2000 ? issue.Message[..2000] : issue.Message });

    private async Task RunReleaseOperationAsync(MigrationOperation op, MigrationOperationRequest request, CancellationToken ct)
    {
        op.Phase = "Checking imported ControlTotals"; await db.SaveChangesAsync(ct);
        var preview = await ValidateAsync(op.ProjectId, new() { Modules = ["ControlTotals"] }, ct);
        if (preview.Errors.Count != 0 || preview.Units.Count != 1 || preview.Units[0].Status != "Already imported" || preview.Units[0].Errors.Count != 0) throw new InvalidOperationException("Upload, validate, approve and import the independent ControlTotals schedule before bulk release.");
        var controls = preview.Units[0].Rows.GroupBy(x => (Kind: x.Get("Kind"), Code: x.Get("Code"))).ToDictionary(x => x.Key, x => x.ToList());
        var itemIds = await db.MigrationOperationItems.AsNoTracking().Where(x => x.OperationId == op.Id && x.Status == "Pending").OrderBy(x => x.Ordinal).Select(x => x.Id).ToListAsync(ct);
        foreach (var itemId in itemIds)
        {
            var current = await db.MigrationOperationItems.AsNoTracking().SingleAsync(x => x.Id == itemId, ct);
            op.Phase = "Releasing pending scopes"; op.CurrentItem = current.Kind + "/" + current.Code; op.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
            try
            {
                using var scope = scopeFactory.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>();
                service.owningOperationId = op.Id; service.operationActor = op.InitiatedBy;
                var childDb = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); using var actor = childDb.EnterMigrationActor(op.InitiatedBy);
                if (!controls.TryGetValue((current.Kind, current.Code), out var rows)) throw new InvalidOperationException("Independent controls are missing.");
                if (rows.GroupBy(x => x.Get("Component")).Any(x => x.Count() != 1)) throw new InvalidOperationException("Duplicate independent control components.");
                // ReleaseAsync commits its outcome checkpoint in the SAME financial transaction.
                await service.ReleaseAsync(op.ProjectId, current.Kind, current.Code, new(request.Evidence, rows.ToDictionary(x => x.Get("Component"), x => x.Decimal("Amount")), rows.ToDictionary(x => x.Get("Component"), x => int.Parse(x.Get("RowCount")))), ct);
            }
            catch (InvalidOperationException ex) { using var scope = scopeFactory.CreateScope(); await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().CompleteOperationItemAsync(op.Id, current.Id, ex.Message, ct); }
            // Reload only small counters changed in the child transaction.
            var saved = await db.MigrationOperations.AsNoTracking().SingleAsync(x => x.Id == op.Id, ct);
            op.Processed = saved.Processed; op.Succeeded = saved.Succeeded; op.NeedsAttention = saved.NeedsAttention;
        }
    }
    private async Task CompleteOperationItemAsync(int operationId, int itemId, string? error, CancellationToken ct)
    {
        var item = await db.MigrationOperationItems.SingleAsync(x => x.Id == itemId && x.OperationId == operationId, ct);
        if (item.Status != "Pending") return;
        var op = await db.MigrationOperations.SingleAsync(x => x.Id == operationId, ct);
        item.Status = error == null ? "Succeeded" : "NeedsAttention"; item.Message = error == null ? "Released" : error[..Math.Min(4000, error.Length)]; item.CompletedAt = DateTime.UtcNow;
        op.Processed++; if (error == null) op.Succeeded++; else op.NeedsAttention++;
        op.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
    }
}
