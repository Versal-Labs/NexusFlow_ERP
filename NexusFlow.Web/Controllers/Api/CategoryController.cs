using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.MasterData.Categories.Commands;
using NexusFlow.AppCore.Features.MasterData.Categories.Queries;
using NexusFlow.Web.Filters;

namespace NexusFlow.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthConstants.HybridPolicy)] // Checks Cookie OR JWT
    [HybridAuthorize]
    public class CategoryController : ControllerBase
    {
        private readonly IMediator _mediator;
        public CategoryController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _mediator.Send(new GetCategoriesQuery()));

        [HttpPost]
        public async Task<IActionResult> Create(CreateCategoryCommand command)
        {
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateCategoryCommand command)
        {
            if (id != command.Category.Id) return BadRequest();
            var result = await _mediator.Send(command);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
