using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.System.CompanyProfileFeature;
using NexusFlow.AppCore.Interfaces;
using System.Threading.Tasks;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    [Authorize(Policy = Permissions.System.ManageConfigs)]
    public class CompanySettingsController : Controller
    {
        private readonly IMediator _mediator;
        private readonly ICompanyProfileService _companyProfileService;
        private readonly IGlobalStorageCoordinator _storageCoordinator;

        public CompanySettingsController(
            IMediator mediator,
            ICompanyProfileService companyProfileService,
            IGlobalStorageCoordinator storageCoordinator)
        {
            _mediator = mediator;
            _companyProfileService = companyProfileService;
            _storageCoordinator = storageCoordinator;
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

        [HttpGet]
        public async Task<IActionResult> Logo()
        {
            var profile = await _companyProfileService.GetProfileAsync(HttpContext.RequestAborted);
            if (string.IsNullOrWhiteSpace(profile.LogoBlobUrl))
            {
                return NotFound();
            }

            try
            {
                var (stream, contentType) = await _storageCoordinator.RetrieveFileAsync(profile.LogoBlobUrl, HttpContext.RequestAborted);
                return File(stream, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            }
            catch
            {
                return NotFound();
            }
        }
    }
}
