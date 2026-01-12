using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.DTOs.Auth;
using NexusFlow.AppCore.Features.Auth.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Infrastructure.Identity;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginCommand command)
        {
            // The Controller doesn't know about UserManager or Tokens.
            // It just asks the Mediator to handle it.
            var result = await _mediator.Send(command);

            return result.Succeeded ? Ok(result) : Unauthorized(result);
        }
    }
}
