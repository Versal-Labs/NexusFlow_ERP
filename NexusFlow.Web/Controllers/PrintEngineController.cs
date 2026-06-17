using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.DTOs.Print;
using NexusFlow.AppCore.Features.Print;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Web.Filters;
using System;
using System.Threading.Tasks;

namespace NexusFlow.Web.Controllers
{
    [Authorize(Policy = AuthConstants.HybridPolicy)]
    [HybridAuthorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PrintEngineController : ControllerBase
    {
        private readonly IDocumentRenderingService _renderingService;
        private readonly IMediator _mediator;

        public PrintEngineController(IDocumentRenderingService renderingService, IMediator mediator)
        {
            _renderingService = renderingService;
            _mediator = mediator;
        }

        [HttpGet("Initialize/{documentType}/{documentId}")]
        public async Task<IActionResult> Initialize(string documentType, string documentId)
        {
            if (!Enum.TryParse<DocumentType>(documentType, true, out var type))
                return BadRequest("Invalid document type.");

            var result = await _mediator.Send(new GetPrintDocumentQuery(type, documentId));
            return result.Succeeded ? Ok(result.Data) : BadRequest(string.Join(", ", result.Errors ?? new[] { result.Message }));
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

        [HttpGet("File/{documentType}/{documentId}")]
        public async Task<IActionResult> DownloadFile(string documentType, string documentId)
        {
            if (!Enum.TryParse<DocumentType>(documentType, true, out var type))
                return BadRequest("Invalid document type.");

            var result = await _mediator.Send(new GetPrintDocumentQuery(type, documentId));
            if (!result.Succeeded)
                return BadRequest(string.Join(", ", result.Errors ?? new[] { result.Message }));

            try
            {
                var actualType = Enum.TryParse<DocumentType>(result.Data.DocumentType, true, out var parsedType) ? parsedType : type;
                var pdfBytes = await _renderingService.RenderDocumentToPdfAsync(actualType, result.Data);
                var fileName = $"{result.Data.DocumentType}-{result.Data.DocumentNumber}.pdf";
                return base.File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error rendering document: {ex.Message}");
            }
        }
    }
}
