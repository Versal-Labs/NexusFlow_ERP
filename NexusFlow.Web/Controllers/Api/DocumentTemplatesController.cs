using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.System.DocumentTemplates;
using NexusFlow.Domain.Enums;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/document-templates")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)]
    [Authorize(Policy = Permissions.System.ManageConfigs)]
    [HybridAuthorize]
    public class DocumentTemplatesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DocumentTemplatesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] DocumentType? documentType)
            => Ok(await _mediator.Send(new GetDocumentTemplatesQuery { DocumentType = documentType }));

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] SaveDocumentTemplateRequest request, CancellationToken cancellationToken)
        {
            var command = await ToCommandAsync(0, request, cancellationToken);
            var result = await _mediator.Send(command, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] SaveDocumentTemplateRequest request, CancellationToken cancellationToken)
        {
            var command = await ToCommandAsync(id, request, cancellationToken);
            var result = await _mediator.Send(command, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:int}/default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var result = await _mediator.Send(new SetDocumentTemplateDefaultCommand(id));
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:int}/active")]
        public async Task<IActionResult> SetActive(int id, [FromBody] SetDocumentTemplateActiveRequest request)
        {
            var result = await _mediator.Send(new SetDocumentTemplateActiveCommand(id, request.IsActive));
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _mediator.Send(new DeleteDocumentTemplateCommand(id));
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        private static async Task<SaveDocumentTemplateCommand> ToCommandAsync(
            int id,
            SaveDocumentTemplateRequest request,
            CancellationToken cancellationToken)
        {
            MemoryStream? fileStream = null;
            if (request.TemplateFile != null && request.TemplateFile.Length > 0)
            {
                fileStream = new MemoryStream();
                await request.TemplateFile.CopyToAsync(fileStream, cancellationToken);
                fileStream.Position = 0;
            }

            return new SaveDocumentTemplateCommand
            {
                Id = id,
                DocumentType = request.DocumentType,
                TemplateName = request.TemplateName,
                TaxProfile = request.TaxProfile,
                IsDefault = request.IsDefault,
                IsActive = request.IsActive,
                TemplateStream = fileStream,
                TemplateFileName = request.TemplateFile?.FileName,
                TemplateContentType = request.TemplateFile?.ContentType
            };
        }
    }

    public class SaveDocumentTemplateRequest
    {
        public DocumentType DocumentType { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public TaxProfile TaxProfile { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
        public IFormFile? TemplateFile { get; set; }
    }

    public class SetDocumentTemplateActiveRequest
    {
        public bool IsActive { get; set; }
    }
}
