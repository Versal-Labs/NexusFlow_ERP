using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Purchasing.Commands;
using NexusFlow.AppCore.Features.Purchasing.PurchaseOrders.Commands;
using NexusFlow.AppCore.Features.Purchasing.PurchaseOrders.Queries;
using NexusFlow.AppCore.Features.Purchasing.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class PurchasingController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PurchasingController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // ==========================================
        // 1. PURCHASE ORDERS
        // ==========================================

        [HttpGet("purchase-orders")]
        public async Task<IActionResult> GetPurchaseOrders()
        {
            var result = await _mediator.Send(new GetPurchaseOrdersQuery());
            return Ok(result);
        }

        [HttpGet("purchase-orders/{id}")]
        public async Task<IActionResult> GetPurchaseOrderById(int id)
        {
            var result = await _mediator.Send(new GetPurchaseOrderByIdQuery { Id = id });
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("purchase-orders")]
        public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // ==========================================
        // 2. GOODS RECEIPT NOTES (GRN)
        // ==========================================

        [HttpGet("grns")]
        public async Task<IActionResult> GetGrns()
        {
            var result = await _mediator.Send(new GetGrnsQuery());
            return Ok(result);
        }

        [HttpPost("grns")]
        public async Task<IActionResult> CreateGrn([FromBody] CreateGrnCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // ==========================================
        // 3. SUPPLIER BILLS (AP INVOICES)
        // ==========================================

        [HttpGet("supplier-bills")]
        public async Task<IActionResult> GetSupplierBills()
        {
            var result = await _mediator.Send(new GetSupplierBillsQuery());
            return Ok(result);
        }

        [HttpPost("supplier-bills")]
        public async Task<IActionResult> CreateSupplierBill([FromBody] CreateSupplierBillCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("suppliers/{supplierId}/unbilled-grns")]
        public async Task<IActionResult> GetUnbilledGrns(int supplierId)
        {
            var result = await _mediator.Send(new GetUnbilledGrnsQuery { SupplierId = supplierId });
            return Ok(result);
        }
    }
}
