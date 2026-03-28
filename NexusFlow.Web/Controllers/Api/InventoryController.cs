using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Inventory.Commands;
using NexusFlow.AppCore.Features.Inventory.Queries;
using NexusFlow.AppCore.Features.Inventory.StockTakes.Commands;
using NexusFlow.AppCore.Features.Inventory.StockTakes.Queries;
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

        [HttpGet("stock/available")]
        public async Task<IActionResult> GetAvailableStock([FromQuery] int variantId, [FromQuery] int warehouseId)
        {
            var result = await _mediator.Send(new GetAvailableStockQuery
            {
                ProductVariantId = variantId,
                WarehouseId = warehouseId
            });
            return Ok(result);
        }

        [HttpGet("stocktakes")]
        public async Task<IActionResult> GetStockTakes() => Ok(await _mediator.Send(new GetStockTakesQuery()));

        [HttpGet("stocktakes/{id}")]
        public async Task<IActionResult> GetStockTakeById(int id)
        {
            var res = await _mediator.Send(new GetStockTakeByIdQuery { Id = id });
            return res.Succeeded ? Ok(res) : NotFound(res.Message);
        }

        [HttpPost("stocktakes/initiate")]
        public async Task<IActionResult> Initiate([FromBody] InitiateStockTakeCommand command)
        {
            var res = await _mediator.Send(command);
            return res.Succeeded ? Ok(res) : BadRequest(res);
        }

        [HttpPost("stocktakes/count")]
        public async Task<IActionResult> SubmitCount([FromBody] SubmitBlindCountCommand command)
        {
            var res = await _mediator.Send(command);
            return res.Succeeded ? Ok(res) : BadRequest(res);
        }

        [HttpPost("stocktakes/{id}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            var res = await _mediator.Send(new ApproveStockTakeCommand { StockTakeId = id, ApproverName = User.Identity?.Name ?? "System" });
            return res.Succeeded ? Ok(res) : BadRequest(res);
        }
    }
}
