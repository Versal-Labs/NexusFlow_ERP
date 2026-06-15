using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Barcodes;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)]
    [HybridAuthorize]
    public class BarcodeController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BarcodeController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("templates")]
        [Authorize(Policy = Permissions.Inventory.PrintBarcodes)]
        public async Task<IActionResult> GetTemplates()
            => Ok(await _mediator.Send(new GetBarcodeTemplatesQuery()));

        [HttpPost("templates")]
        [Authorize(Policy = Permissions.Inventory.ManageBarcodeTemplates)]
        public async Task<IActionResult> CreateTemplate([FromBody] BarcodeTemplateDto template)
        {
            template.Id = 0;
            var result = await _mediator.Send(new SaveBarcodeTemplateCommand { Template = template });
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPut("templates/{id:int}")]
        [Authorize(Policy = Permissions.Inventory.ManageBarcodeTemplates)]
        public async Task<IActionResult> UpdateTemplate(int id, [FromBody] BarcodeTemplateDto template)
        {
            template.Id = id;
            var result = await _mediator.Send(new SaveBarcodeTemplateCommand { Template = template });
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("templates/{id:int}")]
        [Authorize(Policy = Permissions.Inventory.ManageBarcodeTemplates)]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            var result = await _mediator.Send(new DeleteBarcodeTemplateCommand(id));
            return result.Succeeded ? Ok(result) : NotFound(result);
        }

        [HttpGet("variants")]
        [Authorize(Policy = Permissions.Inventory.PrintBarcodes)]
        public async Task<IActionResult> SearchVariants([FromQuery] string? query)
            => Ok(await _mediator.Send(new SearchBarcodeVariantsQuery(query)));

        [HttpPost("generate")]
        [Authorize(Policy = Permissions.Inventory.PrintBarcodes)]
        public async Task<IActionResult> Generate([FromBody] GenerateBarcodePdfQuery query)
        {
            var result = await _mediator.Send(query);
            return result.Succeeded
                ? File(result.Data, "application/pdf")
                : BadRequest(result);
        }
    }
}
