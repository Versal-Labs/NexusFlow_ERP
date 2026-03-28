using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.DTOs.Sales;
using NexusFlow.AppCore.Features.Sales.Commands;
using NexusFlow.AppCore.Features.Sales.CreditNotes.Commands;
using NexusFlow.AppCore.Features.Sales.Orders.Commands;
using NexusFlow.AppCore.Features.Sales.Orders.Queries;
using NexusFlow.AppCore.Features.Sales.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class SalesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SalesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices()
        {
            var result = await _mediator.Send(new GetInvoicesQuery());
            return Ok(result);
        }

        [HttpPost("invoices")]
        // ARCHITECTURAL FIX: Bind directly to the Command, not the Request DTO
        public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("invoices/{id}")]
        public async Task<IActionResult> GetInvoiceById(int id)
        {
            var result = await _mediator.Send(new GetInvoiceByIdQuery { InvoiceId = id });
            return result.Succeeded ? Ok(result) : NotFound(result.Message);
        }

        [HttpPost("credit-notes")]
        public async Task<IActionResult> CreateCreditNote([FromBody] CreateCreditNoteCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("orders/{id}/convert")]
        public async Task<IActionResult> ConvertToInvoice(int id, [FromBody] ConvertOrderToInvoiceCommand command)
        {
            if (id != command.SalesOrderId) return BadRequest("ID mismatch.");

            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var result = await _mediator.Send(new GetSalesOrderByIdQuery { OrderId = id });
            return result.Succeeded ? Ok(result) : NotFound(result.Message);
        }

        [HttpGet("customers/{customerId}/unpaid-invoices")]
        public async Task<IActionResult> GetUnpaid(int customerId)
            => Ok(await _mediator.Send(new GetUnpaidInvoicesQuery { CustomerId = customerId }));

        // ==========================================
        // SALES ORDERS & QUOTATIONS
        // ==========================================
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders()
        {
            return Ok(await _mediator.Send(new GetSalesOrdersQuery()));
        }

        [HttpPost("orders")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateSalesOrderCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("orders/{id}/pdf")]
        [AllowAnonymous] // Optional: Depending on if your JS token logic handles direct window.open
        public async Task<IActionResult> DownloadOrderPdf(int id)
        {
            var result = await _mediator.Send(new GetSalesOrderPdfQuery { OrderId = id });

            if (!result.Succeeded)
                return NotFound(result.Message);

            // Returns a true PDF file stream to the browser
            return File(result.Data, "application/pdf", $"Order_{id}_{DateTime.UtcNow:yyyyMMdd}.pdf");
        }
    }
}
