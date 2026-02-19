using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Purchasing.Commands;
using NexusFlow.AppCore.Features.Purchasing.PurchaseOrders.Commands;
using NexusFlow.AppCore.Features.Purchasing.PurchaseOrders.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class PurchasingController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PurchasingController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _mediator.Send(new GetPurchaseOrdersQuery());
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreatePurchaseOrderCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }


        // POST api/purchasing/grn
        [HttpPost("grn")]
        public async Task<IActionResult> CreateGrn([FromBody] CreateGrnCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _mediator.Send(new GetPurchaseOrderByIdQuery { Id = id });
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

    }
}
