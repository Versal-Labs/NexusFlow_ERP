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
using NexusFlow.AppCore.Features.MasterData.Products.Commands;
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

        [HttpPost("issue-materials")]
        public async Task<IActionResult> IssueMaterials([FromBody] CreateMaterialIssueCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.Succeeded)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        [HttpPost("production-receipts")]
        public async Task<IActionResult> ReceiveProduction([FromBody] ReceiveProductionCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.Succeeded) return Ok(result);
            return BadRequest(result);
        }

        [HttpGet("production-receipts")]
        public async Task<IActionResult> GetProductionReceipts()
        {
            var result = await _mediator.Send(new GetProductionReceiptsQuery());
            // Wrapping in an anonymous object 'new { data = ... }' exactly matches the DataTables expectation
            if (result.Succeeded) return Ok(new { data = result.Data });
            return BadRequest(result);
        }

        [HttpGet("issues")]
        public async Task<IActionResult> GetMaterialIssues()
        {
            var result = await _mediator.Send(new GetMaterialIssuesQuery());
            if (result.Succeeded) return Ok(new { data = result.Data });
            return BadRequest(result);
        }


        //Bulk import

        [HttpPost("preview-import")]
        [AllowAnonymous]
        public async Task<IActionResult> PreviewImport(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return BadRequest("Only CSV supported.");

            using var stream = file.OpenReadStream();
            var result = await _mediator.Send(new PreviewLegacyProductsCommand(stream), cancellationToken);

            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("execute-import")]
        [AllowAnonymous]
        public async Task<IActionResult> ExecuteImport([FromBody] ExecuteLegacyProductsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
