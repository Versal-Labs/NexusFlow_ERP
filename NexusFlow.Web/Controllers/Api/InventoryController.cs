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
using NexusFlow.AppCore.Features.Inventory.ProductionOrders;
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
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> RunProduction([FromBody] RunProductionCommand command)
        {
            return StatusCode(StatusCodes.Status410Gone, new { succeeded = false, code = "legacy_production_disabled", message = "Use /Inventory/ProductionOrders. Free-text production posting is disabled." });
        }

        // POST api/inventory/transfer
        [HttpPost("transfer")]
        [Authorize(Policy = Permissions.Inventory.TransferStock)]
        public async Task<IActionResult> TransferStock([FromBody] TransferStockCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("stock-levels")]
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public async Task<IActionResult> GetStockLevels([FromQuery] int? warehouseId)
        {
            var query = new NexusFlow.AppCore.Features.Inventory.Queries.GetStockLevelsQuery { WarehouseId = warehouseId };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("stock/available")]
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
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
        [Authorize(Policy = Permissions.Inventory.ViewStockTakes)]
        public async Task<IActionResult> GetStockTakes() => Ok(await _mediator.Send(new GetStockTakesQuery()));

        [HttpGet("stocktakes/{id}")]
        [Authorize(Policy = Permissions.Inventory.ViewStockTakes)]
        public async Task<IActionResult> GetStockTakeById(int id)
        {
            var res = await _mediator.Send(new GetStockTakeByIdQuery { Id = id });
            return res.Succeeded ? Ok(res) : NotFound(res.Message);
        }

        [HttpPost("stocktakes/initiate")]
        [Authorize(Policy = Permissions.Inventory.InitiateStockTake)]
        public async Task<IActionResult> Initiate([FromBody] InitiateStockTakeCommand command)
        {
            var res = await _mediator.Send(command);
            return res.Succeeded ? Ok(res) : BadRequest(res);
        }

        [HttpPost("stocktakes/count")]
        [Authorize(Policy = Permissions.Inventory.SubmitCount)]
        public async Task<IActionResult> SubmitCount([FromBody] SubmitBlindCountCommand command)
        {
            var res = await _mediator.Send(command);
            return res.Succeeded ? Ok(res) : BadRequest(res);
        }

        [HttpPost("stocktakes/{id}/approve")]
        [Authorize(Policy = Permissions.Inventory.ApproveStockTake)]
        public async Task<IActionResult> Approve(int id)
        {
            var res = await _mediator.Send(new ApproveStockTakeCommand { StockTakeId = id, ApproverName = User.Identity?.Name ?? "System", ApprovalDate = DateTime.UtcNow.Date });
            return res.Succeeded ? Ok(res) : BadRequest(res);
        }

        [HttpPost("issue-materials")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> IssueMaterials([FromBody] CreateMaterialIssueCommand command)
        {
            return StatusCode(StatusCodes.Status410Gone, new { succeeded = false, code = "legacy_production_disabled", message = "Use /Inventory/ProductionOrders and post against a released production order." });
        }

        [HttpPost("production-receipts")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> ReceiveProduction([FromBody] ReceiveProductionCommand command)
        {
            return StatusCode(StatusCodes.Status410Gone, new { succeeded = false, code = "legacy_production_disabled", message = "Use /Inventory/ProductionOrders and receive against an in-progress production order." });
        }

        [HttpGet("production-orders")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> GetProductionOrders()
        {
            var result = await _mediator.Send(new GetProductionOrdersQuery());
            return result.Succeeded ? Ok(new { data = result.Data }) : BadRequest(result);
        }

        [HttpGet("production-orders/{id:int}")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> GetProductionOrder(int id)
        {
            var result = await _mediator.Send(new GetProductionOrderDetailQuery(id));
            return result.Succeeded ? Ok(result) : NotFound(result);
        }

        [HttpGet("production-orders/{id:int}/history")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> GetProductionOrderHistory(int id) => await GetProductionOrder(id);

        [HttpGet("production-orders/{id:int}/reconciliation")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> GetProductionOrderReconciliation(int id) => await GetProductionOrder(id);

        [HttpGet("production-orders/unbilled-sewing-receipts")]
        [Authorize(Policy = Permissions.Purchasing.ViewBills)]
        public async Task<IActionResult> GetUnbilledSewingReceipts([FromQuery] int supplierId)
        {
            var result = await _mediator.Send(new GetUnbilledProductionReceiptsQuery(supplierId));
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("production-orders")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> CreateProductionOrder([FromBody] CreateProductionOrderCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPut("production-orders/{id:int}")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> UpdateProductionOrder(int id, [FromBody] UpdateProductionOrderCommand command)
        {
            command.Id = id;
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("production-orders/{id:int}/release")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> ReleaseProductionOrder(int id, [FromBody] DateActionRequest request)
        {
            var result = await _mediator.Send(new ReleaseProductionOrderCommand(id, request.Date));
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("production-orders/{id:int}/revision")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> ReviseProductionOrder(int id, [FromBody] ProductionRevisionRequest request)
        {
            var result = await _mediator.Send(new ReviseProductionOrderCommand(id, request.Date, request.TargetQuantity, request.TolerancePercent, request.Reason));
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("production-orders/{id:int}/material-issues")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> IssueProductionOrderMaterials(int id, [FromBody] IssueProductionMaterialsCommand command)
        {
            command.ProductionOrderId = id;
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("production-orders/{id:int}/material-returns")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> ReturnProductionOrderMaterials(int id, [FromBody] ReturnProductionMaterialsCommand command)
        {
            command.ProductionOrderId = id;
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("production-orders/{id:int}/receipts")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> ReceiveProductionOrder(int id, [FromBody] ReceiveProductionOrderCommand command)
        {
            command.ProductionOrderId = id;
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("production-orders/{id:int}/close")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> CloseProductionOrder(int id, [FromBody] DateActionRequest request)
        {
            var result = await _mediator.Send(new CloseProductionOrderCommand(id, request.Date));
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("production-orders/{id:int}/claims/{claimId:int}/settle")]
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public async Task<IActionResult> SettleProductionClaim(int id, int claimId, [FromBody] ClaimSettlementRequest request)
        {
            var result = await _mediator.Send(new SettleProductionClaimCommand(id, claimId, request.Date, request.Reference));
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        public class DateActionRequest { public DateTime Date { get; set; } }
        public class ProductionRevisionRequest
        {
            public DateTime Date { get; set; }
            public decimal TargetQuantity { get; set; }
            public decimal TolerancePercent { get; set; }
            public string Reason { get; set; } = string.Empty;
        }
        public class ClaimSettlementRequest { public DateTime Date { get; set; } public string Reference { get; set; } = string.Empty; }

        [HttpGet("production-receipts")]
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public async Task<IActionResult> GetProductionReceipts()
        {
            var result = await _mediator.Send(new GetProductionReceiptsQuery());
            // Wrapping in an anonymous object 'new { data = ... }' exactly matches the DataTables expectation
            if (result.Succeeded) return Ok(new { data = result.Data });
            return BadRequest(result);
        }

        [HttpGet("issues")]
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public async Task<IActionResult> GetMaterialIssues()
        {
            var result = await _mediator.Send(new GetMaterialIssuesQuery());
            if (result.Succeeded) return Ok(new { data = result.Data });
            return BadRequest(result);
        }


        //Bulk import

        [HttpPost("preview-import")]
        [Authorize(Policy = Permissions.MasterData.ManageProducts)]
        public async Task<IActionResult> PreviewImport(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return BadRequest("Only CSV supported.");

            using var stream = file.OpenReadStream();
            var result = await _mediator.Send(new PreviewLegacyProductsCommand(stream), cancellationToken);

            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("execute-import")]
        [Authorize(Policy = Permissions.MasterData.ManageProducts)]
        public async Task<IActionResult> ExecuteImport([FromBody] ExecuteLegacyProductsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("valuation")]
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public async Task<IActionResult> GetStockValuation([FromQuery] int? warehouseId, [FromQuery] int? categoryId, [FromQuery] string? search)
        {
            var result = await _mediator.Send(new GetStockValuationQuery
            {
                WarehouseId = warehouseId,
                CategoryId = categoryId,
                SearchTerm = search
            });
            return Ok(result);
        }

        [HttpGet("transfers")]
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public async Task<IActionResult> GetTransfers()
        {
            var result = await _mediator.Send(new GetTransfersQuery());
            return Ok(result);
        }

        [HttpPost("transfers")]
        [Authorize(Policy = Permissions.Inventory.TransferStock)]
        public async Task<IActionResult> ExecuteTransfer([FromBody] TransferStockCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("stock-level")]
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public async Task<IActionResult> GetStockLevel([FromQuery] int variantId, [FromQuery] int warehouseId)
        {
            var result = await _mediator.Send(new GetVariantStockLevelQuery
            {
                VariantId = variantId,
                WarehouseId = warehouseId
            });

            return Ok(result);
        }

        [HttpGet("transfers/{refNo}")]
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public async Task<IActionResult> GetTransferByRef(string refNo)
        {
            var result = await _mediator.Send(new GetTransferByRefQuery { ReferenceNo = refNo });
            return result.Succeeded ? Ok(result.Data) : NotFound();
        }

        [HttpGet("transfers/{refNo}/pdf")]
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public IActionResult DownloadTransferPdf(string refNo)
        {
            return Redirect($"/api/PrintEngine/File/StockTransferDeliveryNote/{Uri.EscapeDataString(refNo)}");
        }

        [HttpPost("transfers/{refNo}/reverse")]
        [Authorize(Policy = Permissions.Inventory.TransferStock)]
        public async Task<IActionResult> ReverseTransfer(string refNo)
        {
            var result = await _mediator.Send(new ReverseTransferCommand { ReferenceNo = refNo });
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("adjustments")]
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public async Task<IActionResult> GetAdjustments()
        {
            return Ok(await _mediator.Send(new GetAdjustmentsQuery()));
        }

        [HttpPost("adjustments")]
        [Authorize(Policy = Permissions.Inventory.AdjustStock)]
        public async Task<IActionResult> ExecuteAdjustment([FromBody] AdjustStockCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("opening-stock")]
        [Authorize(Policy = Permissions.Inventory.AdjustStock)]
        public async Task<IActionResult> GetOpeningStock()
        {
            var result = await _mediator.Send(new GetOpeningStockEntriesQuery());
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("opening-stock")]
        [Authorize(Policy = Permissions.Inventory.AdjustStock)]
        public async Task<IActionResult> PostOpeningStock([FromBody] PostOpeningStockCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
