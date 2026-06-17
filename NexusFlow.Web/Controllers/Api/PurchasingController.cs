using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Purchasing.Commands;
using NexusFlow.AppCore.Features.Purchasing.DebitNotes.Commands;
using NexusFlow.AppCore.Features.Purchasing.DebitNotes.Queries;
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
        [Authorize(Policy = Permissions.Purchasing.ViewPOs)]
        public async Task<IActionResult> GetPurchaseOrders()
        {
            var result = await _mediator.Send(new GetPurchaseOrdersQuery());
            return Ok(result);
        }

        [HttpGet("purchase-orders/{id}")]
        [Authorize(Policy = Permissions.Purchasing.ViewPOs)]
        public async Task<IActionResult> GetPurchaseOrderById(int id)
        {
            var result = await _mediator.Send(new GetPurchaseOrderByIdQuery { Id = id });
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("purchase-orders/{id}/pdf")]
        [Authorize(Policy = Permissions.Purchasing.ViewPOs)]
        public IActionResult DownloadPurchaseOrderPdf(int id)
        {
            return Redirect($"/api/PrintEngine/File/PurchaseOrder/{id}");
        }

        [HttpPost("purchase-orders")]
        [Authorize(Policy = Permissions.Purchasing.CreatePO)]
        public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPut("purchase-orders/{id}")]
        [Authorize(Policy = Permissions.Purchasing.CreatePO)]
        public async Task<IActionResult> UpdatePurchaseOrder(int id, [FromBody] UpdatePurchaseOrderCommand command)
        {
            // Standard REST API security check: Ensure the URL ID matches the Payload ID
            if (id != command.Id)
            {
                return BadRequest("The ID in the URL does not match the ID in the payload.");
            }

            var result = await _mediator.Send(command);

            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // ==========================================
        // 2. GOODS RECEIPT NOTES (GRN)
        // ==========================================

        [HttpGet("grns")]
        [Authorize(Policy = Permissions.Purchasing.ViewGRNs)]
        public async Task<IActionResult> GetGrns()
        {
            var result = await _mediator.Send(new GetGrnsQuery());
            return Ok(result);
        }

        [HttpPost("grns")]
        [Authorize(Policy = Permissions.Purchasing.CreateGRN)]
        public async Task<IActionResult> CreateGrn([FromBody] CreateGrnCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("grns/{id}")]
        [Authorize(Policy = Permissions.Purchasing.ViewGRNs)]
        public async Task<IActionResult> GetGrnsById(int id)
        {
            var result = await _mediator.Send(new GetGrnByIdQuery { Id = id });
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("grns/{id}/pdf")]
        [Authorize(Policy = Permissions.Purchasing.ViewGRNs)]
        public IActionResult DownloadGrnPdf(int id)
        {
            return Redirect($"/api/PrintEngine/File/GRN/{id}");
        }

        // ==========================================
        // 3. SUPPLIER BILLS (AP INVOICES)
        // ==========================================

        [HttpGet("supplier-bills")]
        [Authorize(Policy = Permissions.Purchasing.ViewBills)]
        public async Task<IActionResult> GetSupplierBills()
        {
            var result = await _mediator.Send(new GetSupplierBillsQuery());
            return Ok(result);
        }

        [HttpPost("supplier-bills")]
        [Authorize(Policy = Permissions.Purchasing.CreateBill)]
        public async Task<IActionResult> CreateSupplierBill([FromBody] CreateSupplierBillCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("suppliers/{supplierId}/unbilled-grns")]
        [Authorize(Policy = Permissions.Purchasing.ViewGRNs)]
        public async Task<IActionResult> GetUnbilledGrns(int supplierId)
        {
            var result = await _mediator.Send(new GetUnbilledGrnsQuery { SupplierId = supplierId });
            return Ok(result);
        }

        [HttpGet("suppliers/{supplierId}/unpaid-bills")]
        [Authorize(Policy = Permissions.Purchasing.ViewBills)]
        public async Task<IActionResult> GetUnpaidSupplierBills(int supplierId)
        {
            var result = await _mediator.Send(new GetUnpaidSupplierBillsQuery { SupplierId = supplierId });
            return Ok(result);
        }

        [HttpGet("supplier-bills/{id}")]
        [Authorize(Policy = Permissions.Purchasing.ViewBills)]
        public async Task<IActionResult> GetSupplierBillById(int id)
        {
            var result = await _mediator.Send(new GetSupplierBillByIdQuery { Id = id });
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("supplier-bills/{id}/pdf")]
        [Authorize(Policy = Permissions.Purchasing.ViewBills)]
        public IActionResult DownloadSupplierBillPdf(int id)
        {
            return Redirect($"/api/PrintEngine/File/SupplierBill/{id}");
        }

        [HttpPut("supplier-bills/{id}")]
        [Authorize(Policy = Permissions.Purchasing.CreateBill)]
        public async Task<IActionResult> UpdateSupplierBill(int id, [FromBody] UpdateSupplierBillCommand command)
        {
            // Ensure the handler knows exactly which Bill ID it is operating on
            command.Bill.Id = id;

            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // ==========================================
        // 4. Purchase Returns
        // ==========================================

        [HttpGet("debit-notes")]
        [Authorize(Policy = Permissions.Purchasing.ViewDebitNotes)]
        public async Task<IActionResult> GetDebitNotes([FromQuery] int? supplierId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            return Ok(await _mediator.Send(new GetDebitNotesQuery { SupplierId = supplierId, StartDate = startDate, EndDate = endDate }));
        }

        [HttpGet("debit-notes/{id}")]
        [Authorize(Policy = Permissions.Purchasing.ViewDebitNotes)]
        public async Task<IActionResult> GetDebitNoteById(int id)
        {
            var res = await _mediator.Send(new GetDebitNoteByIdQuery { Id = id });
            return res.Succeeded ? Ok(res.Data) : NotFound();
        }

        [HttpGet("debit-notes/{id}/pdf")]
        [Authorize(Policy = Permissions.Purchasing.ViewDebitNotes)]
        public IActionResult DownloadDebitNotePdf(int id)
        {
            return Redirect($"/api/PrintEngine/File/DebitNote/{id}");
        }

        [HttpPost("debit-notes")]
        [Authorize(Policy = Permissions.Purchasing.ViewDebitNotes)]
        public async Task<IActionResult> CreateDebitNote([FromBody] CreateDebitNoteCommand command)
        {
            var res = await _mediator.Send(command);
            return res.Succeeded ? Ok(res) : BadRequest(res);
        }
    }
}
