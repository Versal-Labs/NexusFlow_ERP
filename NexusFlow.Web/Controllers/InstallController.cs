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
        private readonly InstallationRuntimeOptions _runtimeOptions;
        private readonly IInstallationConnectionStringProvider _connectionProvider;
        private readonly IInstallationPreflightChecker _preflight;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<InstallController> _logger;

        public InstallController(
            IInstallationStateStore stateStore,
            IInstallerAccessService access,
            IMediator mediator,
            InstallationPaths paths,
            InstallationRuntimeOptions runtimeOptions,
            IInstallationConnectionStringProvider connectionProvider,
            IInstallationPreflightChecker preflight,
            IHostApplicationLifetime lifetime,
            ILogger<InstallController> logger)
        {
            _stateStore = stateStore;
            _access = access;
            _mediator = mediator;
            _paths = paths;
            _runtimeOptions = runtimeOptions;
            _connectionProvider = connectionProvider;
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

            var model = new InstallViewModel
            {
                Mode = state.Mode,
                IsUnlocked = _access.HasAccess(HttpContext),
                Error = state.LastError,
                PreflightChecks = _preflight.Check(Request),
                LocalStoragePath = _paths.StoragePath,
                CanonicalUrl = $"{Request.Scheme}://{Request.Host}",
                UsePreconfiguredConnectionString = !string.IsNullOrWhiteSpace(_connectionProvider.GetConnectionString())
            };
            PopulateRuntimeModel(model);
            return View(model);
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
                    "No setup key is configured for this instance. Configure NEXUSFLOW_SETUP_KEY or run New-NexusFlowSetupKey.ps1 on a Windows/IIS server.");
                model.Mode = state.Mode;
                model.IsUnlocked = false;
                model.PreflightChecks = _preflight.Check(Request);
                PopulateRuntimeModel(model);
                return View("Index", model);
            }

            if (!_access.TryUnlock(HttpContext, model.SetupKey ?? string.Empty))
            {
                ModelState.AddModelError(string.Empty, "The setup key is invalid or has already been consumed.");
                model.Mode = state.Mode;
                model.IsUnlocked = false;
                model.PreflightChecks = _preflight.Check(Request);
                PopulateRuntimeModel(model);
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

            var preconfiguredConnectionString = model.UsePreconfiguredConnectionString
                ? _connectionProvider.GetConnectionString()
                : null;
            if (model.UsePreconfiguredConnectionString)
            {
                if (string.IsNullOrWhiteSpace(preconfiguredConnectionString))
                {
                    ModelState.AddModelError(nameof(model.UsePreconfiguredConnectionString),
                        "A preconfigured database connection string was selected, but none is available.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(model.Server))
                {
                    ModelState.AddModelError(nameof(model.Server), "SQL Server is required.");
                }

                if (string.IsNullOrWhiteSpace(model.Database))
                {
                    ModelState.AddModelError(nameof(model.Database), "Database name is required.");
                }

                if (!model.UseWindowsAuthentication &&
                    (string.IsNullOrWhiteSpace(model.SqlUsername) || string.IsNullOrWhiteSpace(model.SqlPassword)))
                {
                    ModelState.AddModelError(nameof(model.SqlUsername),
                        "SQL username and password are required when Windows Authentication is not selected.");
                }
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
                PopulateRuntimeModel(model);
                return View("Index", model);
            }

            var request = new InstallationRequest
            {
                Database = new DatabaseConnectionRequest(
                    model.Server, model.Database, model.UseWindowsAuthentication,
                    model.SqlUsername, model.SqlPassword, model.TrustServerCertificate,
                    preconfiguredConnectionString, model.UsePreconfiguredConnectionString),
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
            PopulateRuntimeModel(model);

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

        private void PopulateRuntimeModel(InstallViewModel model)
        {
            model.DeploymentProfile = _runtimeOptions.Profile.ToString();
            model.StorageMode = _runtimeOptions.StorageMode.ToString();
            model.StateStoreMode = _runtimeOptions.StateStoreMode.ToString();
            model.SecretStoreMode = _runtimeOptions.SecretStoreMode.ToString();
            model.PreconfiguredConnectionStringAvailable = !string.IsNullOrWhiteSpace(_connectionProvider.GetConnectionString());
            if (string.IsNullOrWhiteSpace(model.LocalStoragePath))
            {
                model.LocalStoragePath = _paths.StoragePath;
            }
        }
    }
}
