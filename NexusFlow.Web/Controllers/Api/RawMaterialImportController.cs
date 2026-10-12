using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Inventory.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api;

[ApiController]
[Route("api/inventory/raw-material-import")]
[Authorize(Policy = AuthConstants.HybridPolicy)]
[HybridAuthorize]
[Authorize(Policy = Permissions.MasterData.ManageProducts)]
[Authorize(Policy = Permissions.Inventory.AdjustStock)]
[RetiredMigrationEndpoint]
public sealed class RawMaterialImportController(IMediator mediator, IErpDbContext db) : ControllerBase
{
    [HttpGet("setup")]
    public async Task<IActionResult> Setup(CancellationToken ct) => Ok(new
    {
        warehouses = await db.Warehouses.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.Code, x.Name }).ToListAsync(ct),
        brands = await db.Brands.AsNoTracking().OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(ct)
    });

    [HttpGet("template")]
    public IActionResult Template() => File(System.Text.Encoding.UTF8.GetBytes(
        "ProductName,SKU,Quantity,UnitCost\r\nExample raw material,RM-EXAMPLE-001,10,2.50\r\n"),
        "text/csv", "raw-material-stock-template.csv");

    [HttpPost("preview")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public Task<IActionResult> Preview([FromForm] RawMaterialUpload upload, CancellationToken ct) => Process(upload, false, ct);

    [HttpPost("execute")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public Task<IActionResult> Execute([FromForm] RawMaterialUpload upload, CancellationToken ct) => Process(upload, true, ct);

    private async Task<IActionResult> Process(RawMaterialUpload upload, bool execute, CancellationToken ct)
    {
        if (upload.File == null || upload.File.Length == 0 || upload.File.Length > 2 * 1024 * 1024
            || !upload.File.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { succeeded = false, message = "Choose a non-empty CSV file up to 2 MB." });
        if (execute && !upload.Confirmed)
            return BadRequest(new { succeeded = false, message = "Confirm the preview quantities, costs and mappings before posting." });
        await using var stream = upload.File.OpenReadStream();
        var result = await mediator.Send(new ImportRawMaterialStockCommand
        {
            CsvStream = stream, OpeningDate = upload.OpeningDate, WarehouseCode = upload.WarehouseCode,
            BrandName = upload.BrandName, CategoryName = upload.CategoryName, Execute = execute,
            PreviewHash = upload.PreviewHash
        }, ct);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    public sealed class RawMaterialUpload
    {
        public IFormFile? File { get; set; }
        public DateTime OpeningDate { get; set; }
        public string WarehouseCode { get; set; } = "WH-MAIN";
        public string BrandName { get; set; } = "UNBRANDED";
        public string CategoryName { get; set; } = "Accessories";
        public string? PreviewHash { get; set; }
        public bool Confirmed { get; set; }
    }
}
