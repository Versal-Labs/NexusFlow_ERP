using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.MasterData.Products.Commands;
using NexusFlow.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class InventoryImportController : Controller
    {
        private readonly IMediator _mediator;
        private readonly ILogger<InventoryImportController> _logger;

        public InventoryImportController(IMediator mediator, ILogger<InventoryImportController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost("preview-import")]
        public async Task<IActionResult> PreviewImport(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return BadRequest("Only CSV supported.");

            using var stream = file.OpenReadStream();
            var result = await _mediator.Send(new PreviewLegacyProductsCommand(stream), cancellationToken);

            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("execute-import")]
        public async Task<IActionResult> ExecuteImport([FromBody] ExecuteLegacyProductsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
