using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Features.MasterData.Boms.Commands;
using NexusFlow.AppCore.Features.MasterData.Boms.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class BomController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BomController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var result = await _mediator.Send(new GetBomsQuery());
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] BomDto payload)
        {
            var result = await _mediator.Send(new SaveBomCommand(payload));
            if (result.Succeeded) return Ok(result);
            return BadRequest(result);
        }
    }
}
