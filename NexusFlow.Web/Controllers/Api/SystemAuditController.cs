using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Audit.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class SystemAuditController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SystemAuditController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("datatable")]
        public async Task<IActionResult> GetAuditLogsForDataTable([FromQuery] GetSystemAuditLogsQuery query)
        {
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
                return BadRequest(result.Message);

            // DataTables expects data in a specific JSON format
            return Ok(new { data = result.Data });
        }
    }
}
