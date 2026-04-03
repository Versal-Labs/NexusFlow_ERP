using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Purchasing.Suppliers.Commands;
using NexusFlow.AppCore.Features.Purchasing.Suppliers.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class SupplierController : ControllerBase
    {
        private readonly IMediator _mediator;
        public SupplierController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _mediator.Send(new GetSuppliersQuery()));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _mediator.Send(new GetSupplierByIdQuery { Id = id });
            return result.Succeeded ? Ok(result) : NotFound(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateSupplierCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateSupplierCommand command)
        {
            if (id != command.Supplier.Id) return BadRequest("ID Mismatch");
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchSuppliers([FromQuery] string? query)
        {
            var data = await _mediator.Send(new SearchSuppliersQuery(query));
            return Ok(data);
        }
    }
}
