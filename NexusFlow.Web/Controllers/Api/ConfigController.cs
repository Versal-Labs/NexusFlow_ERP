using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Features.Configs.Commands;
using NexusFlow.AppCore.Features.Configs.Queries;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = AuthConstants.HybridScheme)]
    public class ConfigController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ConfigController(IMediator mediator)
        {
            _mediator = mediator;
        }

        #region System Configs (Key-Value)
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            // Returns List<SystemConfigDto>
            var result = await _mediator.Send(new GetSystemConfigsQuery());
            return Ok(result);
        }

        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSetting([FromBody] UpdateSystemConfigCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
        #endregion

        #region Number Sequences
        [HttpGet("sequences")]
        public async Task<IActionResult> GetSequences()
        {
            // Returns List<NumberSequenceDto>
            var result = await _mediator.Send(new GetNumberSequencesQuery());
            return Ok(result);
        }

        [HttpPut("sequences")]
        public async Task<IActionResult> UpdateSequence([FromBody] UpdateNumberSequenceCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
        #endregion
    }
}
