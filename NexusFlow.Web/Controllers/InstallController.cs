using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MediatR;
using NexusFlow.AppCore.Features.Installation;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Infrastructure.Installation;
using NexusFlow.Web.Installation;
using NexusFlow.Web.Models.Installation;

namespace NexusFlow.Web.Controllers
{
    [AllowAnonymous]
    [Route("install")]
    [EnableRateLimiting("installer")]
    public sealed class InstallController : Controller
    {
        private readonly IInstallationStateStore _stateStore;
        private readonly IInstallerAccessService _access;
        private readonly IMediator _mediator;
        private readonly InstallationPaths _paths;
        private readonly IInstallationPreflightChecker _preflight;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<InstallController> _logger;

        public InstallController(
            IInstallationStateStore stateStore,
            IInstallerAccessService access,
            IMediator mediator,
            InstallationPaths paths,
            IInstallationPreflightChecker preflight,
            IHostApplicationLifetime lifetime,
            ILogger<InstallController> logger)
        {
            _stateStore = stateStore;
            _access = access;
            _mediator = mediator;
            _paths = paths;
            _preflight = preflight;
            _lifetime = lifetime;
            _logger = logger;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            var state = _stateStore.Get();
            var maintenanceKeyActive = !state.SetupKeyConsumed &&
                                       !string.IsNullOrWhiteSpace(state.SetupKeyHash) &&
                                       !string.IsNullOrWhiteSpace(state.SetupKeySalt);
            if (state.Mode is ApplicationMode.Installed or ApplicationMode.UpgradeRequired)
            {
                if (_access.HasAccess(HttpContext))
                {
                    return RedirectToAction("Upgrade", "Maintenance");
                }

                if (!maintenanceKeyActive)
                {
                    return RedirectToAction("Login", "Account", new { returnUrl = "/maintenance/upgrade" });
                }
            }

            return View(new InstallViewModel
            {
                Mode = state.Mode,
                IsUnlocked = _access.HasAccess(HttpContext),
                Error = state.LastError,
                PreflightChecks = _preflight.Check(Request),
                LocalStoragePath = _paths.StoragePath,
                CanonicalUrl = $"{Request.Scheme}://{Request.Host}"
            });
        }

        [HttpPost("unlock")]
        [ValidateAntiForgeryToken]
        public IActionResult Unlock(InstallViewModel model)
        {
            var state = _stateStore.Get();
            if (!state.SetupKeyConsumed &&
                (string.IsNullOrWhiteSpace(state.SetupKeyHash) || string.IsNullOrWhiteSpace(state.SetupKeySalt)))
            {
                ModelState.AddModelError(string.Empty,
                    "No setup key is configured for this instance. Run New-NexusFlowSetupKey.ps1 on the server.");
                model.Mode = state.Mode;
                model.IsUnlocked = false;
                model.PreflightChecks = _preflight.Check(Request);
                return View("Index", model);
            }

            if (!_access.TryUnlock(HttpContext, model.SetupKey ?? string.Empty))
            {
                ModelState.AddModelError(string.Empty, "The setup key is invalid or has already been consumed.");
                model.Mode = state.Mode;
                model.IsUnlocked = false;
                model.PreflightChecks = _preflight.Check(Request);
                return View("Index", model);
            }

            if (_stateStore.Get().Mode is ApplicationMode.Installed or ApplicationMode.UpgradeRequired)
            {
                return RedirectToAction("Upgrade", "Maintenance");
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("run")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Run(InstallViewModel model, CancellationToken cancellationToken)
        {
            if (!_access.HasAccess(HttpContext))
            {
                return Unauthorized();
            }

            model.PreflightChecks = _preflight.Check(Request);
            if (model.PreflightChecks.Any(x => !x.Passed))
            {
                ModelState.AddModelError(string.Empty, "Server preflight checks must pass before installation can run.");
            }

            if (!ModelState.IsValid)
            {
                var invalidFields = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .Select(x => string.IsNullOrWhiteSpace(x.Key) ? "server preflight" : x.Key)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                _logger.LogWarning(
                    "Installer submission rejected before provisioning. Invalid fields/checks: {InvalidFields}",
                    invalidFields);
                ModelState.AddModelError(string.Empty,
                    "Installation has not started. Review the validation errors below and submit again.");
                model.IsUnlocked = true;
                model.Mode = _stateStore.Get().Mode;
                return View("Index", model);
            }

            var request = new InstallationRequest
            {
                Database = new DatabaseConnectionRequest(
                    model.Server, model.Database, model.UseWindowsAuthentication,
                    model.SqlUsername, model.SqlPassword, model.TrustServerCertificate),
                CompanyName = model.CompanyName,
                TaxRegistrationNumber = model.TaxRegistrationNumber,
                CanonicalUrl = model.CanonicalUrl,
                TimeZoneId = model.TimeZoneId,
                FiscalYearStart = model.FiscalYearStart,
                FiscalYearEnd = model.FiscalYearEnd,
                VatRate = model.VatRate,
                SsclRate = model.SsclRate,
                WarehouseCode = model.WarehouseCode,
                WarehouseName = model.WarehouseName,
                WarehouseLocation = model.WarehouseLocation ?? string.Empty,
                LocalStoragePath = model.LocalStoragePath,
                AdminFullName = model.AdminFullName,
                AdminEmail = model.AdminEmail,
                AdminPassword = model.AdminPassword
            };

            var result = await _mediator.Send(new RunInstallationCommand(request), cancellationToken);
            model.AdminPassword = string.Empty;
            model.ConfirmAdminPassword = string.Empty;
            model.SqlPassword = string.Empty;
            model.IsUnlocked = true;
            model.Mode = _stateStore.Get().Mode;
            model.Error = result.Succeeded ? null : result.Message;
            model.ReadinessChecks = result.Readiness?.Checks ?? Array.Empty<ReadinessCheck>();

            if (!result.Succeeded)
            {
                return View("Index", model);
            }

            _access.Clear(HttpContext);
            Response.OnCompleted(() =>
            {
                _lifetime.StopApplication();
                return Task.CompletedTask;
            });
            return View("Complete");
        }
    }
}
