using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;

namespace NexusFlow.AppCore.Features.Migration;

public sealed partial class MigrationCentreService(IErpDbContext db, IMigrationFileReader reader,
    IGlobalStorageCoordinator storage, IJournalService journals, ICurrentUserService user,
    IBackgroundJobService jobs, IServiceScopeFactory scopeFactory)
{
    private async Task<MigrationProject> Project(int id, bool writable, CancellationToken ct)
    {
        var p = await db.MigrationProjects.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new InvalidOperationException("Migration project not found.");
        if (writable && p.Status == "Finalized") throw new InvalidOperationException("This migration is finalized and read-only.");
        return p;
    }
    private async Task Editable(MigrationProject p, CancellationToken ct)
    {
        if (await db.MigrationRuns.AnyAsync(x => x.ProjectId == p.Id && (x.Status == "Queued" || x.Status == "Running"), ct)) throw new InvalidOperationException("A posting run is active. Resolve that run before changing this project.");
    }
    private void Changed(MigrationProject p, string action, string detail)
    {
        p.Version++; p.ApprovedHash = null; p.ApprovedAt = null; p.ApprovedBy = null;
        Audit(p.Id, action, detail);
    }
    private void Audit(int id, string action, string detail) => db.MigrationAudits.Add(new MigrationAudit { ProjectId = id, Action = action, Detail = detail });
    public async Task<MigrationProject> CreateAsync(MigrationCreateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120 || string.IsNullOrWhiteSpace(request.SourceSystem) || request.SourceSystem.Trim().Length > 80 || request.OpeningDate.Year < 2000) throw new InvalidOperationException("Enter a project name, stable source system identifier and actual go-live opening date.");
        using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.LockMigrationProjectAsync(0, ct);
        if (await db.MigrationProjects.AnyAsync(ct)) throw new InvalidOperationException("This company already has a migration project. Resume it; a finalized opening position cannot be imported again as a new project.");
        var legacy = await db.CustomerMigrationBatches.Where(x => x.Status == "Posted").ToListAsync(ct);
        if (legacy.Count != 0 && (string.IsNullOrWhiteSpace(request.AdoptionEvidence) || legacy.Any(x => x.CutoverDate.Date != request.OpeningDate.Date || x.SourceAsOfDate.Date != request.OpeningDate.Date.AddDays(-1)))) throw new InvalidOperationException("Existing posted customer batches need explicit adoption evidence and the same opening/closing dates. Use a clean migration target for a different go-live date; retain existing financial history and never import those balances twice.");
        var legacyJournalIds = legacy.Where(x => x.JournalEntryId != null).Select(x => x.JournalEntryId!.Value).ToList();
        if (await db.JournalEntries.AnyAsync(x => !legacyJournalIds.Contains(x.Id), ct)) throw new InvalidOperationException("The migration target already contains financial journals outside the supported customer opening adoption. Use a clean opening ledger and retain training/legacy history separately; do not add another opening position to existing balances.");
        var contributions = await db.JournalLines.Where(x => legacyJournalIds.Contains(x.JournalEntryId)).GroupBy(x => x.AccountId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Debit - x.Credit) }).ToListAsync(ct);
        if ((await db.Accounts.AsNoTracking().ToListAsync(ct)).Any(a => a.Balance != (contributions.SingleOrDefault(x => x.Id == a.Id)?.Amount ?? 0))) throw new InvalidOperationException("Existing account balances do not match the supported adopted journals. Reconcile the target opening ledger before creating this migration.");
        var p = new MigrationProject { Name = request.Name.Trim(), SourceSystem = MigrationJson.Code(request.SourceSystem), OpeningDate = request.OpeningDate.Date, ClosingDate = request.OpeningDate.Date.AddDays(-1), SettingsJson = MigrationJson.Write(new MigrationSettings()) };
        db.MigrationProjects.Add(p); await db.SaveChangesAsync(ct); Audit(p.Id, "Created", $"Opening {p.OpeningDate:yyyy-MM-dd}; legacy closing {p.ClosingDate:yyyy-MM-dd}; LKR; Asia/Colombo.");
        await db.SaveChangesAsync(ct);
        if (legacy.Count != 0) await AdoptLegacyAsync(p, legacy, request.AdoptionEvidence!, ct);
        await tx.CommitAsync(ct); return p;
    }
    public Task<List<MigrationProject>> ListAsync(CancellationToken ct) => db.MigrationProjects.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync(ct);
    public async Task<object> GetAsync(int id, CancellationToken ct)
    {
        var p = await Project(id, false, ct);
        return new { Project = p, Settings = MigrationJson.Read<MigrationSettings>(p.SettingsJson), Datasets = await db.MigrationDatasets.Where(x => x.ProjectId == id).OrderBy(x => x.Module).ThenByDescending(x => x.Revision).Select(x => new { x.Id, x.Module, x.Revision, x.IsCurrent, x.FileName, x.FileHash, x.ClosingDate }).ToListAsync(ct), Scopes = await db.MigrationScopes.Where(x => x.ProjectId == id).ToListAsync(ct), Units = await db.MigrationUnits.Where(x => x.ProjectId == id).Select(x => new { x.Id, x.Module, x.ScopeKey, x.Status, x.JournalEntryId, x.ReversalJournalEntryId }).ToListAsync(ct), Runs = await db.MigrationRuns.Where(x => x.ProjectId == id).OrderByDescending(x => x.Id).ToListAsync(ct), Audit = await db.MigrationAudits.Where(x => x.ProjectId == id).OrderByDescending(x => x.Id).Take(100).ToListAsync(ct) };
    }
    public async Task SaveSettingsAsync(int id, MigrationSettings settings, CancellationToken ct)
    {
        using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.LockMigrationProjectAsync(id, ct);
        var p = await Project(id, true, ct); await Editable(p, ct);
        foreach (var profile in settings.Columns)
            if (!MigrationCatalog.Modules.TryGetValue(profile.Key, out var fields) || profile.Value.Keys.Except(fields).Any()) throw new InvalidOperationException("Column profiles may map only known dataset fields.");
        foreach (var kind in new[] { "Customer", "Supplier", "Stock" })
            if (settings.Mappings.TryGetValue(kind, out var partyMap) && partyMap.Values.Distinct().Count() != partyMap.Count) throw new InvalidOperationException("Each legacy customer/supplier/SKU must map to one distinct master. Merge legacy identities separately before migration.");
        foreach (var module in MigrationCatalog.Modules.Keys)
        {
            if (!settings.DatasetStates.TryGetValue(module, out var state) || state is not "AwaitingData" and not "Provided" and not "NotApplicable") throw new InvalidOperationException($"Set the required data status for {module}.");
            if (state == "NotApplicable" && (!settings.ZeroEvidence.TryGetValue(module, out var evidence) || string.IsNullOrWhiteSpace(evidence))) throw new InvalidOperationException($"{module}: a signed zero/not-applicable declaration is required.");
            if (state == "NotApplicable")
            {
                var provided = await db.MigrationDatasets.Where(x => x.ProjectId == id && x.Module == module && x.IsCurrent).Select(x => x.RowsJson).SingleOrDefaultAsync(ct);
                if (provided != null && MigrationJson.Read<List<MigrationRow>>(provided).Count != 0) throw new InvalidOperationException($"{module}: uploaded rows conflict with a zero declaration. Keep this data required and reconcile it.");
            }
            if (state == "Provided" && !await db.MigrationDatasets.AnyAsync(x => x.ProjectId == id && x.Module == module && x.IsCurrent, ct)) throw new InvalidOperationException($"{module}: upload the dataset first.");
        }
        // Posted unit mappings cannot be silently changed by a later upload.
        if (await db.MigrationUnits.AnyAsync(x => x.ProjectId == id, ct))
        {
            var old = MigrationJson.Read<MigrationSettings>(p.SettingsJson);
            if (old.OpeningEquityAccountId != settings.OpeningEquityAccountId) throw new InvalidOperationException("Opening equity cannot change after posting starts.");
            foreach (var type in old.Mappings) foreach (var pair in type.Value)
                if (!settings.Mappings.TryGetValue(type.Key, out var map) || !map.TryGetValue(pair.Key, out var target) || target != pair.Value) throw new InvalidOperationException("Existing mappings cannot be removed or changed after posting. Additional mappings are allowed.");
        }
        if (settings.SupplierTermDays is < 0 or > 3650) throw new InvalidOperationException("Supplier terms must be from 0 to 3650 days.");
        p.SettingsJson = MigrationJson.Write(settings); Changed(p, "Mappings", "Saved explicit mappings, column profile, defaults and evidence."); await db.SaveChangesAsync(ct); await RefreshStagedScopes(id, settings, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<int> UploadAsync(int id, string module, string filename, byte[] bytes, DateTime closingDate, CancellationToken ct)
    {
        var parsed = reader.Read(module, filename, bytes);
        using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.LockMigrationProjectAsync(id, ct);
        var p = await Project(id, true, ct); await Editable(p, ct);
        if (closingDate.Date != p.ClosingDate || parsed.ReportDate is { } report && report.Date != p.ClosingDate) throw new InvalidOperationException($"This project requires balances at {p.ClosingDate:yyyy-MM-dd}. Refresh the legacy export; a newer/older snapshot cannot be mixed into this opening.");
        foreach (var r in parsed.Rows) if (r.Get("ClosingDate") != "" && r.Date("ClosingDate") != p.ClosingDate || r.Get("SourceSnapshotDate") != "" && r.Date("SourceSnapshotDate") != p.ClosingDate) throw new InvalidOperationException($"Row {r.Row}: snapshot date does not match the project closing date.");
        var current = await db.MigrationDatasets.SingleOrDefaultAsync(x => x.ProjectId == id && x.Module == module && x.IsCurrent, ct);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)); if (current?.FileHash == hash) return current.Id;
        if (current != null) { current.IsCurrent = false; await db.SaveChangesAsync(ct); }
        using var stream = new MemoryStream(bytes);
        var reference = await storage.SaveFileSecurelyAsync(stream, Guid.NewGuid().ToString("N") + Path.GetExtension(filename), "migration/" + id, "application/octet-stream", ct);
        var revision = (await db.MigrationDatasets.Where(x => x.ProjectId == id && x.Module == module).MaxAsync(x => (int?)x.Revision, ct) ?? 0) + 1;
        var dataset = new MigrationDataset { ProjectId = id, Module = module, Revision = revision, FileName = Path.GetFileName(filename), FileHash = hash, StorageReference = reference, ClosingDate = p.ClosingDate, RowsJson = MigrationJson.Write(parsed.Rows), ErrorsJson = MigrationJson.Write(parsed.Errors) };
        db.MigrationDatasets.Add(dataset);
        var settings = MigrationJson.Read<MigrationSettings>(p.SettingsJson); settings.DatasetStates[module] = "Provided"; p.SettingsJson = MigrationJson.Write(settings);
        var kind = MigrationCatalog.ScopeKind(module);
        if (kind != "General")
        {
            var known = await db.MigrationScopes.Where(x => x.ProjectId == id && x.Kind == kind).Select(x => x.Code).ToListAsync(ct);
            foreach (var code in parsed.Rows.Select(x => MigrationJson.Code(x.Get("Code"))).Where(x => x != "").Distinct().Except(known)) db.MigrationScopes.Add(new MigrationScope { ProjectId = id, Kind = kind, Code = code, RequiredModulesJson = MigrationJson.Write(DefaultRequirements(kind)) });
        }
        Changed(p, "Uploaded", $"{module} revision {revision}: {dataset.FileName}, SHA256 {hash}, {parsed.Rows.Count} rows; original retained.");
        await db.SaveChangesAsync(ct); await RefreshStagedScopes(id, settings, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return dataset.Id;
    }
    public async Task<(Stream Stream, string Name)> DownloadAsync(int id, int datasetId, CancellationToken ct)
    {
        await Project(id, false, ct); var d = await db.MigrationDatasets.SingleAsync(x => x.Id == datasetId && x.ProjectId == id, ct);
        return ((await storage.RetrieveFileAsync(d.StorageReference, ct)).Stream, d.FileName);
    }
    public static string[] DefaultRequirements(string kind) => kind switch { "Customer" => ["CustomerOpenings", "ReceivedCheques"], "Supplier" => ["SupplierOpenings", "IssuedCheques"], "Stock" => ["Stock"], "Bank" => ["BankOpening", "BankRecon"], _ => [] };
    private static IEnumerable<MigrationRow> StagedRows(MigrationDataset dataset, MigrationSettings settings)
    {
        var rows = MigrationJson.Read<List<MigrationRow>>(dataset.RowsJson);
        foreach (var row in rows)
        {
            row.Data = new(row.Data, StringComparer.OrdinalIgnoreCase); row.Raw = new(row.Raw, StringComparer.OrdinalIgnoreCase);
            if (settings.Columns.TryGetValue(dataset.Module, out var columns)) foreach (var c in columns) row.Set(c.Key, row.Raw.GetValueOrDefault(c.Value, ""));
            row.Set("Code", MigrationJson.Code(row.Get("Code")));
        }
        return rows;
    }
    private async Task RefreshStagedScopes(int id, MigrationSettings settings, CancellationToken ct)
    {
        var expected = (await db.MigrationDatasets.Where(x => x.ProjectId == id && x.IsCurrent).ToListAsync(ct)).Where(x => MigrationCatalog.ScopeKind(x.Module) != "General")
            .SelectMany(d => StagedRows(d, settings).Where(r => r.Get("Code") != "").Select(r => (Kind: MigrationCatalog.ScopeKind(d.Module), Code: r.Get("Code")))).Distinct().ToList();
        var scopes = await db.MigrationScopes.Where(x => x.ProjectId == id).ToListAsync(ct);
        var scopedModules = MigrationCatalog.Modules.Keys.Where(x => MigrationCatalog.ScopeKind(x) != "General").ToList();
        foreach (var key in expected.Where(k => !scopes.Any(x => x.Kind == k.Kind && x.Code == k.Code))) db.MigrationScopes.Add(new() { ProjectId = id, Kind = key.Kind, Code = key.Code, RequiredModulesJson = MigrationJson.Write(DefaultRequirements(key.Kind)) });
        foreach (var orphan in scopes.Where(x => x.Status == "Pending" && x.EntityId == null && string.IsNullOrEmpty(x.Evidence) && !expected.Contains((x.Kind, x.Code))))
            if (!await db.MigrationUnits.AnyAsync(x => x.ProjectId == id && x.ScopeKey == orphan.Code && scopedModules.Contains(x.Module), ct))
            {
                // Preparation-only placeholders have no business postings. Original revisions stay retained.
                db.MigrationScopes.Remove(orphan); Audit(id, "Staging scope refreshed", $"Removed unused placeholder {orphan.Kind}/{orphan.Code} after source/profile correction.");
            }
    }
    public async Task SaveScopeAsync(int id, MigrationScopeRequest request, CancellationToken ct)
    {
        using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct); await db.LockMigrationProjectAsync(id, ct); var p = await Project(id, true, ct); await Editable(p, ct);
        if (request.Kind is not "Customer" and not "Supplier" and not "Stock" and not "Bank" || string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 100) throw new InvalidOperationException("Choose a valid scope kind and legacy code.");
        var code = MigrationJson.Code(request.Code);
        var scope = await db.MigrationScopes.SingleOrDefaultAsync(x => x.ProjectId == id && x.Kind == request.Kind && x.Code == code, ct);
        if (scope?.Status is "Released" or "NoLegacyOpening") throw new InvalidOperationException("A released scope cannot be changed; use a dated correction.");
        var required = DefaultRequirements(request.Kind);
        if (request.RequiredModules.Count != required.Length || request.RequiredModules.Except(required).Any() || required.Except(request.RequiredModules).Any()) throw new InvalidOperationException("Keep exactly the supported opening components for this scope. Use an evidenced NotApplicable declaration instead of removing a component.");
        if (request.NoLegacyOpening && string.IsNullOrWhiteSpace(request.Evidence)) throw new InvalidOperationException("Record evidence that this is a new-system entity with no legacy opening.");
        if (request.NoLegacyOpening && await db.MigrationUnits.AnyAsync(x => x.ProjectId == id && x.ScopeKey == code && x.Status == "Posted" && MigrationCatalog.FinancialModules.Contains(x.Module), ct)) throw new InvalidOperationException("A migrated entity cannot be classified as having no legacy opening.");
        if (request.NoLegacyOpening)
        {
            foreach (var dataset in await db.MigrationDatasets.Where(x => x.ProjectId == id && x.IsCurrent).ToListAsync(ct))
                if (MigrationCatalog.FinancialModules.Contains(dataset.Module) && MigrationCatalog.ScopeKind(dataset.Module) == request.Kind && StagedRows(dataset, MigrationJson.Read<MigrationSettings>(p.SettingsJson)).Any(r => r.Get("Code") == code)) throw new InvalidOperationException("Uploaded legacy financial/stock rows exist for this code. Import and reconcile its opening position instead of classifying it as a new-system entity.");
        }
        if (scope == null) { scope = new MigrationScope { ProjectId = id, Kind = request.Kind, Code = code }; db.MigrationScopes.Add(scope); }
        scope.RequiredModulesJson = MigrationJson.Write(request.RequiredModules); scope.Evidence = request.Evidence;
        scope.EntityId = await EntityId(request.Kind, code, MigrationJson.Read<MigrationSettings>(p.SettingsJson), ct);
        if (request.NoLegacyOpening && scope.EntityId != null && await db.MigrationScopes.AnyAsync(x => x.ProjectId == id && x.Kind == request.Kind && x.Code != code && x.EntityId == scope.EntityId, ct)) throw new InvalidOperationException("This master already belongs to another migration scope. An alternate code cannot bypass its opening lock.");
        if (request.NoLegacyOpening && scope.EntityId != null)
        {
            var actual = request.Kind switch { "Customer" => await db.Customers.Where(x => x.Id == scope.EntityId).Select(x => x.LegacyCustomerCode).SingleAsync(ct), "Supplier" => await db.Suppliers.Where(x => x.Id == scope.EntityId).Select(x => x.LegacySupplierCode).SingleAsync(ct), "Stock" => await db.ProductVariants.Where(x => x.Id == scope.EntityId).Select(x => x.SKU).SingleAsync(ct), _ => await db.Accounts.Where(x => x.Id == scope.EntityId).Select(x => x.Code).SingleAsync(ct) };
            if (actual != null && MigrationJson.Code(actual) != code) throw new InvalidOperationException("Classify a new-system entity using its actual master code; a legacy-code alias cannot bypass another scope.");
        }
        if (request.NoLegacyOpening) { if (scope.EntityId == null) throw new InvalidOperationException("Create the new-system master first, then classify its exact code/ID mapping."); scope.Status = "NoLegacyOpening"; scope.ReleasedAt = DateTime.UtcNow; }
        Changed(p, "Scope", $"{scope.Kind}/{scope.Code}: {scope.Status}; {request.Evidence}"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task<int?> EntityId(string kind, string code, MigrationSettings s, CancellationToken ct)
    {
        if (kind == "Bank" && s.Mappings.TryGetValue("Account", out var accountMap) && accountMap.TryGetValue(code, out var accountId))
        { if (!await db.Accounts.AnyAsync(x => x.Id == accountId && x.IsActive && x.IsTransactionAccount, ct)) throw new InvalidOperationException("Mapped bank account is missing or inactive."); return accountId; }
        if (s.Mappings.TryGetValue(kind, out var map) && map.TryGetValue(code, out var id))
        {
            var exists = kind switch { "Customer" => await db.Customers.AnyAsync(x => x.Id == id, ct), "Supplier" => await db.Suppliers.AnyAsync(x => x.Id == id, ct), "Stock" => await db.ProductVariants.AnyAsync(x => x.Id == id, ct), "Bank" => await db.Accounts.AnyAsync(x => x.Id == id, ct), _ => false };
            if (!exists) throw new InvalidOperationException("The mapped scope master does not exist."); return id;
        }
        return kind switch { "Customer" => await db.Customers.Where(x => x.LegacyCustomerCode == code).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct), "Supplier" => await db.Suppliers.Where(x => x.LegacySupplierCode == code).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct), "Stock" => await db.ProductVariants.Where(x => x.SKU == code).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct), "Bank" => await db.Accounts.Where(x => x.Code == code).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct), _ => null };
    }
    public static byte[] Templates()
    {
        using var result = new MemoryStream();
        using (var zip = new ZipArchive(result, ZipArchiveMode.Create, true))
        {
            foreach (var module in MigrationCatalog.Modules) { using var w = new StreamWriter(zip.CreateEntry(module.Key + ".csv").Open(), new UTF8Encoding(false)); w.WriteLine(string.Join(',', module.Value.Append("ClosingDate"))); }
            using (var manifest = new StreamWriter(zip.CreateEntry("manifest.json").Open())) manifest.Write(MigrationJson.Write(new { Version = MigrationCatalog.Version, SourceSystem = "ENTER_STABLE_SOURCE_ID", ClosingDate = "yyyy-MM-dd", Files = MigrationCatalog.Modules.Keys.Select(x => new { Module = x, File = x + ".csv" }) }));
            using var readme = new StreamWriter(zip.CreateEntry("README.txt").Open()); readme.Write("NexusFlow migration template v1.0. UTF-8 CSV / values-only XLSX. Codes, cheque numbers and references are TEXT. Dates yyyy-MM-dd, LKR amounts signed with 2 decimals, quantities 4 decimals, unit costs 6 decimals. Positive customer/supplier Amount = debt; negative = credit/advance. Export ALL unsettled documents for each party, including offsetting items with a zero net balance. ClosingDate must be the evening before actual go-live (Asia/Colombo). Stable LegacyRecordId strongly recommended. Blank contacts are allowed and flagged. Stock requires all warehouses and FIFO layers, or explicit summarized-layer approval. Cheques: PartyReflected and BankReflected must explicitly state true/false. Received Status = InSafe, Deposited or Endorsed. Issued cheques are pending bank items, not new payments. BankRecon Amount uses bank statement adjustment signs: bank book = statement + outstanding deposits - uncleared payments. Do not add an issued/received cheque a second time as BankRecon. Source trial balance must include every ledger account, including zero balances. Setup accounts, warehouses, UOM, categories, brands and bank branches in normal master maintenance, then save exact mappings in Migration Centre. No historical invoices or receipts are recreated. Empty templates are not evidence of zero; record a signed NotApplicable declaration for modules with no opening balance.");
        }
        return result.ToArray();
    }
}
