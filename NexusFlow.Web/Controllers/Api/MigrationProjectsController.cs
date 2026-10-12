using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Migration;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api;

[ApiController, Route("api/migration/projects")]
[Authorize(Policy = AuthConstants.HybridPolicy), HybridAuthorize]
[Authorize(Policy = Permissions.Migration.View)]
public sealed class MigrationProjectsController(MigrationCentreService service, IErpDbContext db, IMigrationFileReader reader, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet] public Task<IActionResult> List(CancellationToken ct) => Run(async () => await service.ListAsync(ct));
    [HttpGet("{id:int}")] public Task<IActionResult> Get(int id, CancellationToken ct) => Run(async () => await service.GetAsync(id, ct));
    [HttpPost, Authorize(Policy = Permissions.Migration.Prepare)]
    public Task<IActionResult> Create(MigrationCreateRequest request, CancellationToken ct) => Run(async () => await service.CreateAsync(request, ct));
    [HttpGet("templates")]
    public IActionResult Templates() => File(MigrationCentreService.Templates(), "application/zip", "NexusFlow-migration-templates-v1.0.zip");
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups(CancellationToken ct) => Ok(new {
        modules = MigrationCatalog.Modules.Keys, fields = MigrationCatalog.Modules,
        Account = await db.Accounts.AsNoTracking().Where(x => x.IsActive && x.IsTransactionAccount).Select(x => new { x.Id, Code = x.Code, Label = x.Code + " — " + x.Name, Type = x.Type.ToString() }).ToListAsync(ct),
        Customer = await db.Customers.AsNoTracking().Select(x => new { x.Id, Code = x.LegacyCustomerCode ?? "", Label = x.Name }).ToListAsync(ct),
        Supplier = await db.Suppliers.AsNoTracking().Select(x => new { x.Id, Code = x.LegacySupplierCode ?? "", Label = x.Name }).ToListAsync(ct),
        Stock = await db.ProductVariants.AsNoTracking().Select(x => new { x.Id, Code = x.SKU, Label = x.SKU + " — " + x.Name }).ToListAsync(ct),
        Warehouse = await db.Warehouses.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Id, Code = x.Code, Label = x.Name }).ToListAsync(ct),
        Unit = await db.UnitOfMeasures.AsNoTracking().Select(x => new { x.Id, Code = x.Symbol, Label = x.Name }).ToListAsync(ct),
        Brand = await db.Brands.AsNoTracking().Select(x => new { x.Id, Code = x.Name, Label = x.Name }).ToListAsync(ct),
        Category = await db.Categories.AsNoTracking().Select(x => new { x.Id, Code = x.Code, Label = x.Name }).ToListAsync(ct),
        BankBranch = await db.BankBranches.AsNoTracking().Where(x => x.IsActive && x.Bank.IsActive).Select(x => new { x.Id, Code = x.Bank.BankCode + "-" + x.BranchCode, Label = x.Bank.Name + " / " + x.BranchName }).ToListAsync(ct),
        lookups = await db.SystemLookups.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Id, x.Type, Code = x.Code, Label = x.Value }).ToListAsync(ct)
    });
    [HttpGet("{id:int}/columns")]
    public async Task<IActionResult> Columns(int id, CancellationToken ct)
    {
        var data = await db.MigrationDatasets.Where(x => x.ProjectId == id && x.IsCurrent).ToListAsync(ct);
        return Ok(data.ToDictionary(x => x.Module, x => MigrationJson.Read<List<MigrationRow>>(x.RowsJson).SelectMany(r => r.Raw.Keys).Distinct().OrderBy(k => k).ToList()));
    }
    [HttpPut("{id:int}/mappings"), Authorize(Policy = Permissions.Migration.Prepare)]
    public Task<IActionResult> Settings(int id, MigrationSettings request, CancellationToken ct) => Run(async () => { await service.SaveSettingsAsync(id, request, ct); return await service.GetAsync(id, ct); });
    [HttpPut("{id:int}/scope"), Authorize(Policy = Permissions.Migration.Prepare)]
    public async Task<IActionResult> Scope(int id, MigrationScopeRequest request, CancellationToken ct)
    {
        if (request.NoLegacyOpening && !(await authorization.AuthorizeAsync(User, Permissions.Migration.Approve)).Succeeded) return Forbid();
        return await Run(async () => { await service.SaveScopeAsync(id, request, ct); return await service.GetAsync(id, ct); });
    }
    [HttpPost("{id:int}/upload"), RequestSizeLimit(22 * 1024 * 1024), Authorize(Policy = Permissions.Migration.Prepare)]
    public Task<IActionResult> Upload(int id, [FromForm] MigrationUpload request, CancellationToken ct) => Run(async () => new { datasetId = await service.UploadAsync(id, request.Module, request.File.FileName, await Bytes(request.File, ct), request.ClosingDate, ct) });
    [HttpPost("{id:int}/package"), RequestSizeLimit(22 * 1024 * 1024), Authorize(Policy = Permissions.Migration.Prepare)]
    public Task<IActionResult> Package(int id, IFormFile file, CancellationToken ct) => Run(async () => {
        using var zip = new ZipArchive(new MemoryStream(await Bytes(file, ct)), ZipArchiveMode.Read);
        if (zip.Entries.Count > 25 || zip.Entries.Sum(x => x.Length) > 100 * 1024 * 1024 || zip.Entries.Select(x => x.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != zip.Entries.Count) throw new InvalidDataException("Package exceeds limits or contains duplicate entries.");
        var entry = zip.GetEntry("manifest.json") ?? throw new InvalidDataException("Package needs manifest.json."); if (entry.Length > 32000) throw new InvalidDataException("Manifest exceeds 32 KB.");
        using var manifestReader = new StreamReader(entry.Open()); var manifest = MigrationJson.Read<MigrationManifest>(await manifestReader.ReadToEndAsync(ct));
        var p = await db.MigrationProjects.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        if (manifest.Version != MigrationCatalog.Version || MigrationJson.Code(manifest.SourceSystem) != p.SourceSystem || manifest.ClosingDate.Date != p.ClosingDate || manifest.Files.Count == 0 || manifest.Files.Count > MigrationCatalog.Modules.Count || manifest.Files.Select(x => x.Module).Distinct().Count() != manifest.Files.Count) throw new InvalidDataException("Manifest version, source system, closing date and unique modules must match this project.");
        var uploads = new List<(string Module, string File, byte[] Bytes)>();
        foreach (var item in manifest.Files)
        {
            if (item.File != Path.GetFileName(item.File) || item.File.Contains('\\') || item.File.Contains('/')) throw new InvalidDataException("Package files must have plain file names.");
            var source = zip.GetEntry(item.File) ?? throw new InvalidDataException("Manifest file missing: " + item.File); if (source.Length > 20 * 1024 * 1024) throw new InvalidDataException("An individual file exceeds 20 MB.");
            using var sourceStream = source.Open(); using var buffer = new MemoryStream(); await sourceStream.CopyToAsync(buffer, ct); var bytes = buffer.ToArray();
            var parsed = reader.Read(item.Module, item.File, bytes); if (parsed.ReportDate is { } date && date != p.ClosingDate || parsed.Errors.Count != 0) throw new InvalidDataException("A package dataset has an incorrect report date or parsing errors."); uploads.Add((item.Module, item.File, bytes));
        }
        var saved = new List<int>(); foreach (var item in uploads) saved.Add(await service.UploadAsync(id, item.Module, item.File, item.Bytes, p.ClosingDate, ct)); return new { datasetIds = saved };
    });
    [HttpGet("{id:int}/source/{datasetId:int}")]
    public async Task<IActionResult> Source(int id, int datasetId, CancellationToken ct) { var source = await service.DownloadAsync(id, datasetId, ct); return File(source.Stream, "application/octet-stream", source.Name); }
    [HttpPost("{id:int}/validate"), Authorize(Policy = Permissions.Migration.Prepare)]
    public Task<IActionResult> Validate(int id, MigrationSelection request, CancellationToken ct) => Run(async () => await service.ValidateAsync(id, request, ct));
    [HttpPost("{id:int}/approve"), Authorize(Policy = Permissions.Migration.Approve)]
    public Task<IActionResult> Approve(int id, MigrationApproveRequest request, CancellationToken ct) => Run(async () => { await service.ApproveAsync(id, request, ct); return new { approved = true }; });
    [HttpPost("{id:int}/execute"), Authorize(Policy = Permissions.Migration.Execute)]
    public Task<IActionResult> Execute(int id, MigrationSelection request, CancellationToken ct) => Run(async () => new { runId = await service.ExecuteAsync(id, request, ct) });
    [HttpPost("{id:int}/runs/{runId:int}/resume"), Authorize(Policy = Permissions.Migration.Execute)]
    public Task<IActionResult> Resume(int id, int runId, CancellationToken ct) => Run(async () => { await service.DispatchAsync(id, runId, ct); return new { queued = true }; });
    [HttpGet("{id:int}/reconciliation/{kind}/{code}")]
    public Task<IActionResult> Reconcile(int id, string kind, string code, CancellationToken ct) => Run(async () => await service.ReconcileScopeAsync(id, kind, code, ct));
    [HttpPost("{id:int}/release/{kind}/{code}"), Authorize(Policy = Permissions.Migration.Approve)]
    public Task<IActionResult> Release(int id, string kind, string code, MigrationReleaseRequest request, CancellationToken ct) => Run(async () => { await service.ReleaseAsync(id, kind, code, request, ct); return new { released = true }; });
    [HttpGet("{id:int}/reconciliation")]
    public Task<IActionResult> ReconcileLedger(int id, CancellationToken ct) => Run(async () => await service.ReconcileLedgerAsync(id, ct));
    [HttpPost("{id:int}/release-selected"), Authorize(Policy = Permissions.Migration.Approve)]
    public Task<IActionResult> ReleaseSelected(int id, MigrationBulkReleaseRequest request, CancellationToken ct) => Run(async () => await service.ReleaseSelectedAsync(id, request, ct));
    [HttpPost("{id:int}/finalize"), Authorize(Policy = Permissions.Migration.Finalize)]
    public Task<IActionResult> Finalize(int id, MigrationReleaseRequest request, CancellationToken ct) => Run(async () => { await service.FinalizeAsync(id, request.Evidence, ct); return new { finalized = true }; });
    [HttpPost("{id:int}/units/{unitId:int}/reverse"), Authorize(Policy = Permissions.Migration.Execute)]
    public Task<IActionResult> Reverse(int id, int unitId, MigrationReversalRequest request, CancellationToken ct) => Run(async () => { await service.ReverseUnitAsync(id, unitId, request, ct); return new { reversed = true }; });
    private async Task<IActionResult> Run(Func<Task<object>> action)
    {
        try { return Ok(await action()); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Project changed in another session. Refresh and review it again." }); }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or FormatException or System.Text.Json.JsonException) { return BadRequest(new { message = ex.Message }); }
    }
    private static async Task<byte[]> Bytes(IFormFile file, CancellationToken ct) { if (file == null || file.Length is <= 0 or > 20 * 1024 * 1024) throw new InvalidDataException("Choose a nonempty file up to 20 MB."); using var output = new MemoryStream(); await file.CopyToAsync(output, ct); return output.ToArray(); }
    public sealed class MigrationUpload { public string Module { get; set; } = ""; public DateTime ClosingDate { get; set; } public IFormFile File { get; set; } = null!; }
    public sealed class MigrationManifest { public string Version { get; set; } = ""; public string SourceSystem { get; set; } = ""; public DateTime ClosingDate { get; set; } public List<ManifestFile> Files { get; set; } = []; }
    public sealed record ManifestFile(string Module, string File);
}

[ApiController, Route("api/migration/status")]
[Authorize(Policy = AuthConstants.HybridPolicy), HybridAuthorize]
public sealed class MigrationStatusController(IErpDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Status(CancellationToken ct) => Ok(new { provisional = await db.MigrationProjects.AnyAsync(x => x.Status != "Finalized", ct) });
}
