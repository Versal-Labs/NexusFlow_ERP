using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.DTOs.Print;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using System;
using System.Threading.Tasks;

namespace NexusFlow.Web.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PrintEngineController : ControllerBase
    {
        private readonly IDocumentRenderingService _renderingService;

        public PrintEngineController(IDocumentRenderingService renderingService)
        {
            _renderingService = renderingService;
        }

        [HttpGet("Initialize/{documentType}/{documentId}")]
        public IActionResult Initialize(string documentType, string documentId)
        {
            // In a real application, you would dispatch a MediatR query to fetch the actual
            // document details (e.g. GetSalesInvoicePrintDtoQuery).
            // For now, we return a mock DTO to initialize the form.
            var dummyData = new PrintDocumentDto
            {
                DocumentId = documentId,
                DocumentType = documentType,
                DocumentNumber = $"{documentType.ToUpper()}-{documentId}",
                DocumentDate = DateTime.UtcNow,
                CustomerOrSupplierName = "Sample Customer",
                BillingAddress = "123 Business Road\nCity, Country",
                SubTotal = 1000m,
                TaxTotal = 150m,
                DiscountTotal = 0m,
                GrandTotal = 1150m,
                LineItems = new System.Collections.Generic.List<PrintLineItemDto>
                {
                    new PrintLineItemDto { ItemCode = "ITM-001", Description = "Sample Item", Quantity = 10, UnitPrice = 100, LineTotal = 1000 }
                }
            };
            return Ok(dummyData);
        }

        [HttpPost("Render")]
        public async Task<IActionResult> Render([FromBody] PrintDocumentDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.DocumentType))
                return BadRequest("Invalid document data.");

            if (!Enum.TryParse<DocumentType>(dto.DocumentType, true, out var type))
                return BadRequest("Invalid document type.");

            try
            {
                var pdfBytes = await _renderingService.RenderDocumentToPdfAsync(type, dto);
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error rendering document: {ex.Message}");
            }
        }
    }
}
