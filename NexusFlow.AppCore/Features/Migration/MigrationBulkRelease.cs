using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace NexusFlow.AppCore.Features.Migration;

public sealed record MigrationBulkReleaseResult(int Released, List<string> Pending);
public sealed partial class MigrationCentreService
{
    public async Task<MigrationBulkReleaseResult> ReleaseSelectedAsync(int id, MigrationBulkReleaseRequest request, CancellationToken ct)
    {
        using var worker = await db.AcquireMigrationWorkerAsync(id, ct);
        await Project(id, true, ct);
        if (string.IsNullOrWhiteSpace(request.Evidence)) throw new InvalidOperationException("Signed complete-position and independent control schedule evidence is required.");
        var preview = await ValidateAsync(id, new() { Modules = ["ControlTotals"] }, ct);
        if (preview.Errors.Count != 0 || preview.Units.Count != 1 || preview.Units[0].Status != "Already imported" || preview.Units[0].Errors.Count != 0) throw new InvalidOperationException("Upload, validate, approve and import the independent ControlTotals schedule before bulk release.");
        var controls = preview.Units[0].Rows;
        var kinds = request.Selection.Modules.Select(MigrationCatalog.ScopeKind).Where(x => x != "General").Distinct().ToList();
        var scopes = await db.MigrationScopes.AsNoTracking().Where(x => x.ProjectId == id && x.Status == "Pending" && kinds.Contains(x.Kind)).ToListAsync(ct);
        var pending = new List<string>(); var released = 0;
        foreach (var scope in scopes)
        {
            var filter = scope.Kind switch { "Customer" => request.Selection.Customers, "Supplier" => request.Selection.Suppliers, "Stock" => request.Selection.Skus, _ => request.Selection.Banks };
            if (filter.Count != 0 && !filter.Select(MigrationJson.Code).Contains(scope.Code)) continue;
            var rows = controls.Where(r => r.Get("Kind") == scope.Kind && r.Get("Code") == scope.Code).ToList();
            if (rows.Count == 0) { pending.Add($"{scope.Kind}/{scope.Code}: independent controls are missing."); continue; }
            if (rows.GroupBy(x => x.Get("Component")).Any(x => x.Count() != 1)) { pending.Add($"{scope.Kind}/{scope.Code}: duplicate independent control components."); continue; }
            try
            {
                using var releaseScope = scopeFactory.CreateScope();
                var service = releaseScope.ServiceProvider.GetRequiredService<MigrationCentreService>(); service.owningLegacyRelease = true;
                await service.ReleaseAsync(id, scope.Kind, scope.Code, new(request.Evidence, rows.ToDictionary(r => r.Get("Component"), r => r.Decimal("Amount")), rows.ToDictionary(r => r.Get("Component"), r => int.Parse(r.Get("RowCount")))), ct); released++;
            }
            catch (InvalidOperationException ex) { pending.Add($"{scope.Kind}/{scope.Code}: {ex.Message}"); }
        }
        return new(released, pending);
    }
}
