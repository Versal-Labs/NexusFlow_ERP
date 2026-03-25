using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Sales.Commissions;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class CommissionRuleController : ControllerBase
    {
        private readonly IMediator _mediator;
        public CommissionRuleController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _mediator.Send(new GetCommissionRulesQuery()));

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SaveCommissionRuleCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
