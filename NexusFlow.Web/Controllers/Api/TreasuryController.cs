using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Treasury.Commands;
using NexusFlow.AppCore.Features.Treasury.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)]
    [HybridAuthorize]
    public class TreasuryController : ControllerBase
    {
        private readonly IMediator _mediator;
        public TreasuryController(IMediator mediator) => _mediator = mediator;

        [HttpGet("receipts")]
        public async Task<IActionResult> GetReceipts() => Ok(await _mediator.Send(new GetReceiptsQuery()));

        [HttpPost("payments")]
        public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
