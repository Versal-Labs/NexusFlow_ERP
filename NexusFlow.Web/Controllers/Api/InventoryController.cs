using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Inventory.Commands;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class InventoryController : ControllerBase
    {
        private readonly IMediator _mediator;

        public InventoryController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // POST api/inventory/production
        [HttpPost("production")]
        public async Task<IActionResult> RunProduction([FromBody] RunProductionCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // POST api/inventory/transfer
        [HttpPost("transfer")]
        public async Task<IActionResult> TransferStock([FromBody] TransferStockCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("stock-levels")]
        public async Task<IActionResult> GetStockLevels([FromQuery] int? warehouseId)
        {
            var query = new NexusFlow.AppCore.Features.Inventory.Queries.GetStockLevelsQuery { WarehouseId = warehouseId };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
    }
}
