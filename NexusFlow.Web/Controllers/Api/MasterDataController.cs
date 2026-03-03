using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.MasterData.Products.Queries;
using NexusFlow.AppCore.Features.MasterData.Warehouses.Commands;
using NexusFlow.AppCore.Features.MasterData.Warehouses.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class MasterDataController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MasterDataController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
            => Ok(await _mediator.Send(new GetProductsQuery()));

        [HttpGet("suppliers")]
        public async Task<IActionResult> GetSuppliers()
            => Ok(await _mediator.Send(new GetSuppliersQuery()));

        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers()
            => Ok(await _mediator.Send(new GetCustomersQuery()));

        [HttpGet("warehouses")]
        public async Task<IActionResult> GetWarehouses() => Ok(await _mediator.Send(new AppCore.Features.MasterData.Warehouses.Queries.GetWarehousesQuery()));

        [HttpGet("warehouses/{id}")]
        public async Task<IActionResult> GetWarehouse(int id) => Ok(await _mediator.Send(new GetWarehouseByIdQuery { Id = id }));

        [HttpPost("warehouses")]
        [HttpPut("warehouses/{id}")]
        public async Task<IActionResult> SaveWarehouse([FromBody] SaveWarehouseCommand command, int? id = null)
        {
            if (id.HasValue) command.Warehouse.Id = id.Value;
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
