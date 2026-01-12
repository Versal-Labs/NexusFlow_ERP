using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Features.Purchasing.Commands;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = AuthConstants.HybridScheme)]
    public class PurchasingController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PurchasingController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // POST api/purchasing/grn
        [HttpPost("grn")]
        public async Task<IActionResult> CreateGrn([FromBody] CreateGrnCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // TODO: Add endpoint for CreatePurchaseOrderCommand when you are ready
    }
}
