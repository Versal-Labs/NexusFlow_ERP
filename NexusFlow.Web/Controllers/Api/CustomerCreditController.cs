using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Treasury.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Services;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api;

[ApiController, Route("api/treasury/customer-credits")]
[Authorize(Policy = AuthConstants.HybridPolicy), HybridAuthorize]
public sealed class CustomerCreditController(CustomerSettlementService settlement, IErpDbContext db) : ControllerBase
{
    [HttpGet("{customerId:int}")]
    [Authorize(Policy = Permissions.Treasury.ViewReceipts)]
    public async Task<IActionResult> Applications(int customerId, CancellationToken ct) => Ok(await db.CustomerCreditApplications.AsNoTracking()
        .Where(x => x.CreditOpeningItem.CustomerId == customerId).OrderByDescending(x => x.Id)
        .Select(x => new { x.Id, x.Date, x.Amount, x.ReversedDate, x.ReversalReason, CreditReference = x.CreditOpeningItem.ReferenceNo,
            TargetReference = x.SalesInvoiceId != null ? x.SalesInvoice!.InvoiceNumber : x.CustomerOpeningItemId != null ? x.CustomerOpeningItem!.ReferenceNo : x.CustomerDebitMemo!.DebitMemoNumber }).ToListAsync(ct));
    [HttpPost("apply")]
    [Authorize(Policy = Permissions.Treasury.CreateReceipt)]
    public async Task<IActionResult> Apply(ApplyRequest request, CancellationToken ct)
    {
        try { return Ok(new { succeeded = true, data = await settlement.ApplyCreditAsync(request.CustomerId, request.CreditOpeningItemId, request.Date, request.Allocations, ct) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "Concurrent settlement. Refresh the available balances." }); }
    }
    [HttpPost("{id:int}/reverse")]
    [Authorize(Policy = Permissions.Treasury.VoidReceipt)]
    public async Task<IActionResult> Reverse(int id, ReverseRequest request, CancellationToken ct)
    {
        try { await settlement.ReverseCreditAsync(id, request.Date, request.Reason, ct); return Ok(new { succeeded = true }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "Concurrent settlement. Refresh and retry." }); }
    }
    public sealed record ApplyRequest(int CustomerId, int CreditOpeningItemId, DateTime Date, List<PaymentAllocationRequest> Allocations);
    public sealed record ReverseRequest(DateTime Date, string Reason);
}
