using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Features.MasterData.Boms.Commands;
using NexusFlow.AppCore.Features.MasterData.Boms.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class BomController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BomController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _mediator.Send(new GetBomsQuery());
            return Ok(new { data = result.Data }); // DataTables format
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _mediator.Send(new GetBomByIdQuery(id));
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost]
        public async Task<IActionResult> Save(SaveBomCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
