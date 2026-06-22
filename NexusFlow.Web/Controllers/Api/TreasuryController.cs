using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Treasury.Commands;
using NexusFlow.AppCore.Features.Treasury.Queries;
using NexusFlow.Domain.Enums;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)]
    [HybridAuthorize]
    public class TreasuryController : ControllerBase
    {
        private readonly IMediator _mediator;
        public TreasuryController(IMediator mediator) => _mediator = mediator;

        // =======================================================
        // INCOMING CASH (Accounts Receivable - Customers)
        // =======================================================

        [HttpGet("receipts")]
        [Authorize(Policy = Permissions.Treasury.ViewReceipts)]
        public async Task<IActionResult> GetReceipts() => Ok(await _mediator.Send(new GetReceiptsQuery()));

        [HttpPost("receipts")]
        [Authorize(Policy = Permissions.Treasury.CreateReceipt)]
        public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("receipts/{id}")]
        [Authorize(Policy = Permissions.Treasury.ViewReceipts)]
        public async Task<IActionResult> GetReceiptById(int id)
        {
            var result = await _mediator.Send(new GetReceiptByIdQuery { ReceiptId = id });
            if (result.Succeeded) return Ok(result.Data);
            return NotFound(result.Message);
        }

        [HttpGet("receipts/{id}/pdf")]
        [Authorize(Policy = Permissions.Treasury.ViewReceipts)]
        public IActionResult DownloadReceiptPdf(int id)
        {
            return Redirect($"/api/PrintEngine/File/CustomerReceipt/{id}");
        }

        [HttpPost("receipts/{id}/void")]
        [Authorize(Policy = Permissions.Treasury.VoidReceipt)]
        public async Task<IActionResult> VoidReceipt(int id, [FromQuery] DateTime? date)
        {
            var result = await _mediator.Send(new VoidReceiptCommand(id, date ?? DateTime.UtcNow.Date));
            if (result.Succeeded) return Ok(result);
            return BadRequest(result);
        }

        // =======================================================
        // OUTGOING CASH (Accounts Payable - Suppliers)
        // =======================================================

        [HttpGet("payments")]
        [Authorize(Policy = Permissions.Treasury.ViewPayments)]
        public async Task<IActionResult> GetPayments([FromQuery] int? type, [FromQuery] int? supplierId, [FromQuery] int? method)
        {
            // This feeds your Supplier Payments Datatable
            var result = await _mediator.Send(new GetPaymentsQuery { Type = type, SupplierId = supplierId, Method = method });
            return Ok(result);
        }

        [HttpPost("payments")]
        [Authorize(Policy = Permissions.Treasury.CreatePayment)]
        public async Task<IActionResult> RecordSupplierPayment([FromBody] RecordSupplierPaymentCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("payments/{id}")]
        [Authorize(Policy = Permissions.Treasury.ViewPayments)]
        public async Task<IActionResult> GetSupplierPaymentById(int id)
        {
            var result = await _mediator.Send(new GetSupplierPaymentByIdQuery { PaymentId = id });
            if (result.Succeeded) return Ok(result.Data);
            return NotFound(result.Message);
        }

        [HttpGet("payments/{id}/remittance")]
        [Authorize(Policy = Permissions.Treasury.ViewPayments)]
        public IActionResult DownloadSupplierPaymentRemittance(int id)
        {
            return Redirect($"/api/PrintEngine/File/SupplierPaymentRemittance/{id}");
        }

        // =======================================================
        // CHEQUE VAULT MANAGEMENT
        // =======================================================

        [HttpGet("cheques")]
        [Authorize(Policy = Permissions.Treasury.ManageCheques)]
        public async Task<IActionResult> GetCheques([FromQuery] int? customerId, [FromQuery] int? bankId, [FromQuery] int? branchId, [FromQuery] int? status)
        {
            var result = await _mediator.Send(new GetChequesQuery { CustomerId = customerId, BankId = bankId, BankBranchId = branchId, Status = (ChequeStatus?)status });
            return Ok(result);
        }

        [HttpPost("cheques/bounce")]
        [Authorize(Policy = Permissions.Treasury.ManageCheques)]
        public async Task<IActionResult> BounceCheque([FromBody] BounceChequeCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("cheques/endorse")]
        [Authorize(Policy = Permissions.Treasury.ManageCheques)]
        public async Task<IActionResult> EndorseCheque([FromBody] EndorseChequeCommand command)
        {
            // Triggers when a user endorses a cheque directly from the Vault UI
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("payments/endorse")]
        [Authorize(Policy = Permissions.Treasury.CreatePayment)]
        public async Task<IActionResult> EndorseChequePayment([FromBody] EndorseChequeCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("cheques/{id:int}/clear")]
        [Authorize(Policy = Permissions.Treasury.ManageCheques)]
        public async Task<IActionResult> ClearEndorsedCheque(int id, [FromBody] ClearEndorsedChequeRequest request)
        {
            var result = await _mediator.Send(new ClearEndorsedChequeCommand(id, request.ClearedDate));
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }

    public sealed class ClearEndorsedChequeRequest
    {
        public DateTime ClearedDate { get; set; }
    }
}
