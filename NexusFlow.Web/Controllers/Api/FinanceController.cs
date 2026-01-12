using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Features.Finance.Commands;
using NexusFlow.AppCore.Features.Finance.Queries;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = AuthConstants.HybridScheme)]
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

        [HttpPost("account")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountCommand command)
        {
            var result = await _mediator.Send(command);

            if (result.Succeeded)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        [HttpGet("trial-balance")]
        public async Task<IActionResult> GetTrialBalance([FromQuery] DateTime? date)
        {
            var query = new GetTrialBalanceQuery { AsOfDate = date ?? DateTime.UtcNow };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("balance-sheet")]
        public async Task<IActionResult> GetBalanceSheet([FromQuery] DateTime? date)
        {
            var query = new GetBalanceSheetQuery { AsOfDate = date ?? DateTime.UtcNow };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
    }
}
