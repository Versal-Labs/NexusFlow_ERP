using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.System.IAM.Commands;
using NexusFlow.AppCore.Features.System.IAM.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class IamController : ControllerBase
    {
        private readonly IMediator _mediator;
        public IamController(IMediator mediator) => _mediator = mediator;

        [HttpGet("users")]
        [Authorize(Policy = Permissions.System.ViewUsers)]
        public async Task<IActionResult> GetUsers()
        {
            return Ok(await _mediator.Send(new GetUsersQuery()));
        }

        [HttpGet("roles")]
        [Authorize(Policy = Permissions.System.ManageRoles)]
        public async Task<IActionResult> GetRoles()
        {
            var result = await _mediator.Send(new GetRolesQuery());
            return Ok(result.Data);
        }

        [HttpPost("users")]
        [Authorize(Policy = Permissions.System.ManageUsers)]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("users/{id}/toggle-status")]
        [Authorize(Policy = Permissions.System.ManageUsers)]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            var result = await _mediator.Send(new ToggleUserStatusCommand { UserId = id });
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("roles")]
        [Authorize(Policy = Permissions.System.ManageRoles)]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPut("users/{id}")]
        [Authorize(Policy = Permissions.System.ManageUsers)]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserCommand command)
        {
            command.UserId = id;
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPut("roles/{id}")]
        [Authorize(Policy = Permissions.System.ManageRoles)]
        public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateRoleCommand command)
        {
            command.RoleId = id;
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("permissions")]
        [Authorize(Policy = Permissions.System.ManageRoles)]
        public async Task<IActionResult> GetAllPermissions()
        {
            return Ok(await _mediator.Send(new GetAllPermissionsQuery()));
        }

        [HttpGet("roles/{id}/permissions")]
        [Authorize(Policy = Permissions.System.ManageRoles)]
        public async Task<IActionResult> GetRolePermissions(string id)
        {
            var result = await _mediator.Send(new GetRolePermissionsQuery { RoleId = id });
            return result.Succeeded ? Ok(result.Data) : BadRequest(result.Message);
        }

        [HttpPost("roles/{id}/permissions")]
        [Authorize(Policy = Permissions.System.ManageRoles)]
        public async Task<IActionResult> UpdateRolePermissions(string id, [FromBody] List<string> permissions)
        {
            var result = await _mediator.Send(new UpdateRolePermissionsCommand { RoleId = id, Permissions = permissions });
            return result.Succeeded ? Ok(result) : BadRequest(result.Message);
        }
    }
}
