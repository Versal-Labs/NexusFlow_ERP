using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Features.System.CompanyProfileFeature;
using System.Threading.Tasks;

namespace NexusFlow.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin")] // Assuming SuperAdmin role manages company settings
    public class CompanySettingsController : Controller
    {
        private readonly IMediator _mediator;

        public CompanySettingsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var result = await _mediator.Send(new GetCompanyProfileQuery());
            if (result.Succeeded)
            {
                return View(result.Data);
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(UpdateCompanyProfileCommand command, IFormFile? logoFile)
        {
            if (logoFile != null && logoFile.Length > 0)
            {
                command.LogoStream = logoFile.OpenReadStream();
                command.LogoFileName = logoFile.FileName;
                command.LogoContentType = logoFile.ContentType;
            }

            var result = await _mediator.Send(command);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                ModelState.AddModelError(string.Empty, string.Join(", ", result.Errors ?? new[] { result.Message }));
            }

            // Reload data
            var profileResult = await _mediator.Send(new GetCompanyProfileQuery());
            return View(profileResult.Data);
        }
    }
}
