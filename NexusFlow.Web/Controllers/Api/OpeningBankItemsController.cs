using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api;

[ApiController, Route("api/treasury/opening-bank-items")]
[Authorize(Policy = AuthConstants.HybridPolicy), HybridAuthorize]
[Authorize(Policy = Permissions.Treasury.ManageCheques)]
public sealed class OpeningBankItemsController(IErpDbContext db, NexusFlow.AppCore.Services.OpeningBankItemService items) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var active = db.MigrationUnits.Where(x => x.Status == "Posted").Select(x => x.Id);
        return Ok(await db.OpeningBankItems.AsNoTracking().Where(x => x.ClearedDate == null && x.CancelledDate == null && active.Contains(x.MigrationUnitId)).Select(x => new { x.Id, x.BankAccountId, x.SupplierId, x.Reference, x.Kind, x.Date, x.Amount, x.ChequeNumber, x.ChequeDate }).ToListAsync(ct));
    }
    [HttpPost("{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id, NexusFlow.AppCore.Services.OpeningBankResolution request, CancellationToken ct)
    {
        try { await items.ResolveAsync(id, request, ct); return Ok(new { resolved = true }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

[ApiController, Route("api/treasury/supplier-opening-credits")]
[Authorize(Policy = AuthConstants.HybridPolicy), HybridAuthorize]
[Authorize(Policy = Permissions.Treasury.CreatePayment)]
public sealed class SupplierOpeningCreditsController(IErpDbContext db, NexusFlow.AppCore.Services.SupplierBalanceService balances) : ControllerBase
{
    [HttpGet("{supplierId:int}")]
    public async Task<IActionResult> List(int supplierId, CancellationToken ct)
    {
        var units = await db.MigrationUnits.Where(x => x.Status == "Posted").Select(x => x.Id).ToListAsync(ct);
        var items = await db.SupplierOpeningItems.Where(x => x.SupplierId == supplierId && x.Amount < 0 && units.Contains(x.MigrationUnitId)).ToListAsync(ct);
        var applications = await db.SupplierCreditApplications.ToListAsync(ct);
        return Ok(items.Select(x => new { x.Id, x.ReferenceNo, Available = -x.Amount - applications.Where(a => a.CreditOpeningItemId == x.Id).Sum(a => a.Amount) }).Where(x => x.Available > 0));
    }
    [HttpPost("apply")]
    public async Task<IActionResult> Apply(Application request, CancellationToken ct)
    {
        try { await balances.ApplyCreditAsync(request.CreditId, request.OpeningItemId, request.BillId, request.Amount, request.Date, ct); return Ok(new { applied = true }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
    public sealed record Application(int CreditId, int? OpeningItemId, int? BillId, decimal Amount, DateTime Date);
}
