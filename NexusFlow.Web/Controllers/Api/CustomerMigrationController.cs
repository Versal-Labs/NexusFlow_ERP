using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Finance.CustomerMigration;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api;

[ApiController, Route("api/finance/customer-migration")]
[Authorize(Policy = AuthConstants.HybridPolicy), HybridAuthorize]
[Authorize(Policy = Permissions.Finance.ManageAccounts)]
[Authorize(Policy = Permissions.Treasury.ManageCheques)]
[RetiredMigrationEndpoint]
public sealed class CustomerMigrationController(CustomerMigrationService migration, CustomerMasterImportService masters, IErpDbContext db) : ControllerBase
{
    [HttpGet("setup")]
    public async Task<IActionResult> Setup(CancellationToken ct) => Ok(new {
        postingEnabled = await db.SystemConfigs.AnyAsync(x => x.Key == CustomerMigrationService.EnableKey && x.Value == "true", ct),
        signedOff = await db.CustomerMigrationBatches.AnyAsync(x => x.Status == "Posted" && x.IsSignedOff, ct),
        branches = await db.BankBranches.Include(x => x.Bank).AsNoTracking().Where(x => x.Bank.IsActive && x.IsActive)
            .OrderBy(x => x.Bank.Name).ThenBy(x => x.BranchName).Select(x => new { x.Id, Label = x.Bank.BankCode + "-" + x.BranchCode + " " + x.Bank.Name + " / " + x.BranchName }).ToListAsync(ct),
        lookups = await db.SystemLookups.AsNoTracking().Where(x => x.IsActive && (x.Type == "CustomerGroup" || x.Type == "PriceLevel" || x.Type == "PaymentTerm"))
            .Select(x => new { x.Id, x.Type, x.Value }).ToListAsync(ct),
        accounts = await db.Accounts.AsNoTracking().Where(x => x.IsTransactionAccount).Select(x => new { x.Id, x.Code, x.Name, x.Type }).ToListAsync(ct),
        salesReps = await db.Employees.AsNoTracking().Where(x => x.IsSalesRep).Select(x => new { x.Id, Code = x.EmployeeCode, FullName = x.FirstName + " " + x.LastName }).ToListAsync(ct)
    });
    [HttpGet("batches")]
    public async Task<IActionResult> Batches(CancellationToken ct) => Ok(await db.CustomerMigrationBatches.AsNoTracking().OrderByDescending(x => x.Id)
        .Select(x => new { x.Id, x.Status, x.CutoverDate, x.SourceAsOfDate, x.IsSignedOff, x.PostedAt, x.JournalEntryId, x.OutstandingFileName, x.ChequeFileName }).Take(100).ToListAsync(ct));
    [HttpPost("posting-setting")]
    public async Task<IActionResult> PostingSetting(PostingSettingRequest request, CancellationToken ct) => await Run(async () => {
        if (request.Enabled && !request.UatApproved) throw new InvalidOperationException("Confirm UAT approval before enabling migration posting.");
        if (request.Enabled && await db.CustomerMigrationBatches.AnyAsync(x => x.Status == "Posted" && x.IsSignedOff, ct)) throw new InvalidOperationException("Migration is signed off; posting cannot be re-enabled.");
        var config = await db.SystemConfigs.FirstOrDefaultAsync(x => x.Key == CustomerMigrationService.EnableKey, ct);
        if (config == null) { config = new SystemConfig { Key = CustomerMigrationService.EnableKey, DataType = "Boolean", Description = "Customer opening migration posting; enable only after approved UAT" }; db.SystemConfigs.Add(config); }
        config.Value = request.Enabled ? "true" : "false"; await db.SaveChangesAsync(ct);
        return new { postingEnabled = request.Enabled };
    });
    [HttpPost("preview"), RequestSizeLimit(22 * 1024 * 1024)]
    public async Task<IActionResult> Preview([FromForm] Upload request, CancellationToken ct) => await Run(async () => {
        var outstanding = await Read(request.Outstanding, [".xls", ".xlsx"], ct);
        var cheques = await Read(request.Cheques, [".xlsx", ".xls"], ct);
        return await migration.CreateAsync(outstanding, request.Outstanding!.FileName, cheques, request.Cheques!.FileName, request.CutoverDate, request.SourceAsOfDate, ct);
    });
    [HttpGet("{id:int}")]
    public Task<IActionResult> Get(int id, CancellationToken ct) => Run(async () => await migration.GetAsync(id, ct));
    [HttpPost("{id:int}/review")]
    public Task<IActionResult> Review(int id, Dictionary<string, int> mappings, CancellationToken ct) => Run(async () => await migration.ReviewAsync(id, mappings, ct));
    [HttpPost("{id:int}/post")]
    public Task<IActionResult> Post(int id, PostRequest request, CancellationToken ct) => Run(async () => await migration.PostAsync(id, request.ReviewHash, request.PhysicallyVerified, ct));
    [HttpGet("{id:int}/reconciliation")]
    public Task<IActionResult> Reconcile(int id, CancellationToken ct) => Run(async () => await migration.ReconcileAsync(id, ct));
    [HttpPost("{id:int}/sign-off")]
    public Task<IActionResult> SignOff(int id, CancellationToken ct) => Run(async () => { await migration.SignOffAsync(id, ct); return await migration.GetAsync(id, ct); });
    [HttpPost("{id:int}/reverse")]
    public Task<IActionResult> Reverse(int id, ReverseRequest request, CancellationToken ct) => Run(async () => { await migration.ReverseAsync(id, request.Date, request.Reason, ct); return await migration.GetAsync(id, ct); });
    [HttpGet("{id:int}/source/{kind}")]
    public async Task<IActionResult> Source(int id, string kind, CancellationToken ct)
    {
        var batch = await db.CustomerMigrationBatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (batch == null || kind is not ("outstanding" or "cheques")) return NotFound();
        return File(kind == "outstanding" ? batch.OutstandingFile : batch.ChequeFile, "application/octet-stream", kind == "outstanding" ? batch.OutstandingFileName : batch.ChequeFileName);
    }
    [HttpGet("{id:int}/audit.csv")]
    public async Task<IActionResult> Audit(int id, CancellationToken ct)
    {
        var review = await migration.GetAsync(id, ct);
        static string Cell(string? s) { s ??= ""; if (s.Length > 0 && "=+-@\t\r\n".Contains(s[0])) s = "'" + s; return "\"" + s.Replace("\"", "\"\"") + "\""; }
        var csv = new StringBuilder("LegacyCode,Customer,Debit,Credit,NetAR,ChequesInHand\r\n");
        foreach (var row in review.Totals) csv.AppendLine(string.Join(',', Cell(row.Code), Cell(row.Name), row.Debit.ToString(System.Globalization.CultureInfo.InvariantCulture), row.Credit.ToString(System.Globalization.CultureInfo.InvariantCulture), row.NetAr.ToString(System.Globalization.CultureInfo.InvariantCulture), row.Cheques.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"customer-migration-{id}-totals.csv");
    }
    [HttpGet("masters/template")]
    [Authorize(Policy = Permissions.MasterData.ManageCustomers)]
    public IActionResult Template() => File(Encoding.UTF8.GetBytes(CustomerMasterImportService.Headers + "\r\n"), "text/csv", "customer-master-template.csv");
    [HttpPost("masters/preview"), RequestSizeLimit(11 * 1024 * 1024)]
    [Authorize(Policy = Permissions.MasterData.ManageCustomers)]
    public Task<IActionResult> MasterPreview([FromForm] MasterUpload request, CancellationToken ct) => Run(async () => await masters.PreviewAsync(await Read(request.File, [".csv"], ct), ct));
    [HttpPost("masters/import"), RequestSizeLimit(11 * 1024 * 1024)]
    [Authorize(Policy = Permissions.MasterData.ManageCustomers)]
    public Task<IActionResult> MasterImport([FromForm] MasterUpload request, CancellationToken ct) => Run(async () => {
        if (!request.Confirmed) throw new InvalidOperationException("Confirm the customer master preview.");
        return await masters.ExecuteAsync(await Read(request.File, [".csv"], ct), request.PreviewHash, ct);
    });
    private async Task<IActionResult> Run(Func<Task<object>> operation)
    {
        try { return Ok(await operation()); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "Concurrent or duplicate data change. Reload and preview again." }); }
    }
    private static async Task<byte[]> Read(IFormFile? file, string[] extensions, CancellationToken ct)
    {
        if (file == null || file.Length is <= 0 or > 10485760 || !extensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) throw new InvalidOperationException("Choose a supported non-empty file up to 10 MB.");
        using var data = new MemoryStream(); await file.CopyToAsync(data, ct); return data.ToArray();
    }
    public sealed class Upload { public IFormFile? Outstanding { get; set; } public IFormFile? Cheques { get; set; } public DateTime CutoverDate { get; set; } public DateTime SourceAsOfDate { get; set; } }
    public sealed record PostRequest(string ReviewHash, bool PhysicallyVerified);
    public sealed record ReverseRequest(DateTime Date, string Reason);
    public sealed record PostingSettingRequest(bool Enabled, bool UatApproved);
    public sealed class MasterUpload { public IFormFile? File { get; set; } public string PreviewHash { get; set; } = ""; public bool Confirmed { get; set; } }
}
