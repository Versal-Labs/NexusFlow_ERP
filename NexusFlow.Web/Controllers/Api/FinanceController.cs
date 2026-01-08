using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Features.Finance.Queries;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class FinanceController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FinanceController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("chart-of-accounts")]
        public async Task<IActionResult> GetChartOfAccounts()
        {
            var result = await _mediator.Send(new GetChartOfAccountsQuery());

            if (result.Succeeded)
                return Ok(result.Data);

            return BadRequest(result.Errors);
        }
    }
}
