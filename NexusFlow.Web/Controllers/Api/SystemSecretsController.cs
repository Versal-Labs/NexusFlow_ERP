using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.System.Secrets;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/system/secrets")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)]
    [Authorize(Policy = Permissions.SuperAdmin)]
    [HybridAuthorize]
    public class SystemSecretsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SystemSecretsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus()
        {
            var result = await _mediator.Send(new GetSecretSettingsStatusQuery());
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("test")]
        public async Task<IActionResult> Test([FromBody] TestSecretSettingRequest request)
        {
            var result = await _mediator.Send(new TestSecretSettingCommand
            {
                Key = request.Key,
                Value = request.Value
            });

            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] SaveSecretSettingRequest request)
        {
            var result = await _mediator.Send(new SaveSecretSettingCommand
            {
                Key = request.Key,
                Value = request.Value,
                CurrentPassword = request.CurrentPassword
            });

            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("remove")]
        public async Task<IActionResult> Remove([FromBody] RemoveSecretSettingRequest request)
        {
            var result = await _mediator.Send(new RemoveSecretSettingCommand
            {
                Key = request.Key,
                CurrentPassword = request.CurrentPassword
            });

            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("rotate-jwt")]
        public async Task<IActionResult> RotateJwt([FromBody] SecretPasswordConfirmationRequest request)
        {
            var result = await _mediator.Send(new RotateJwtSecretCommand
            {
                CurrentPassword = request.CurrentPassword
            });

            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("restart")]
        public async Task<IActionResult> Restart([FromBody] SecretPasswordConfirmationRequest request)
        {
            var result = await _mediator.Send(new RequestApplicationRestartCommand
            {
                CurrentPassword = request.CurrentPassword
            });

            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
