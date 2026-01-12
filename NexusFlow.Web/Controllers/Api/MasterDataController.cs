using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Features.MasterData.Queries;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = AuthConstants.HybridScheme)]
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
        public async Task<IActionResult> GetWarehouses()
            => Ok(await _mediator.Send(new GetWarehousesQuery()));
    }
}
