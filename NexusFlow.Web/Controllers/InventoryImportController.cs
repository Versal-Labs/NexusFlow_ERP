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

        [HttpPost("UploadAndImport")]
        public async Task<IActionResult> UploadAndImport(IFormFile file, CancellationToken cancellationToken)
        {
            // 1. Validate the file
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "No file was uploaded or the file is empty." });
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Only CSV files are supported. If using Excel, save as .csv first." });
            }

            try
            {
                // 2. Open a read stream from the uploaded file
                using var stream = file.OpenReadStream();

                // 3. Create the MediatR Command
                var command = new ImportLegacyProductsCommand(stream);

                // 4. Send to your Handler
                var result = await _mediator.Send(command, cancellationToken);

                // 5. Check your custom Result<T> wrapper
                if (result.Succeeded)
                {
                    return Ok(new
                    {
                        success = true,
                        message = string.IsNullOrWhiteSpace(result.Message) ? "Import successful" : result.Message,
                        importedCount = result.Data
                    });
                }
                else
                {
                    // Combine the errors array into a single string for the frontend, or fallback to Message
                    string errorMessage = result.Errors != null && result.Errors.Length > 0
                        ? string.Join(" | ", result.Errors)
                        : result.Message ?? "Import failed due to an unknown error.";

                    return BadRequest(new { success = false, message = errorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while importing legacy products.");
                return StatusCode(500, new { success = false, message = "An internal server error occurred during import. Check logs for details." });
            }
        }
    }
}
