using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Installation;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Web.Installation;

namespace NexusFlow.Web.Controllers
{
    [Route("maintenance")]
    public sealed class MaintenanceController : Controller
    {
        private readonly IInstallationDatabaseProvisioner _database;
        private readonly IMediator _mediator;
        private readonly IInstallerAccessService _installerAccess;
        private readonly IInstallationStateStore _stateStore;
        private readonly IHostApplicationLifetime _lifetime;

        public MaintenanceController(
            IInstallationDatabaseProvisioner database,
            IMediator mediator,
            IInstallerAccessService installerAccess,
            IInstallationStateStore stateStore,
            IHostApplicationLifetime lifetime)
        {
            _database = database;
            _mediator = mediator;
            _installerAccess = installerAccess;
            _stateStore = stateStore;
            _lifetime = lifetime;
        }

        [HttpGet("upgrade")]
        [AllowAnonymous]
        public async Task<IActionResult> Upgrade(CancellationToken cancellationToken)
        {
            if (!IsAuthorized())
            {
                if (User.Identity?.IsAuthenticated == true)
                {
                    return RedirectToAction("AccessDenied", "Account");
                }

                return RedirectToAction("Login", "Account", new { returnUrl = "/maintenance/upgrade" });
            }

            return View(await _database.GetPendingMigrationsAsync(cancellationToken));
        }

        [HttpPost("upgrade")]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Upgrade(bool backupConfirmed, CancellationToken cancellationToken)
        {
            if (!IsAuthorized())
            {
                return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
            }

            if (!backupConfirmed)
            {
                ModelState.AddModelError(string.Empty, "Confirm that a current database backup exists before upgrading.");
                return View(await _database.GetPendingMigrationsAsync(cancellationToken));
            }

            var result = await _mediator.Send(new RunInstallationUpgradeCommand(), cancellationToken);
            if (!result.Succeeded)
            {
                ViewBag.Error = result.Message;
                return View(await _database.GetPendingMigrationsAsync(cancellationToken));
            }

            if (!_stateStore.Get().SetupKeyConsumed)
            {
                await _stateStore.ConsumeSetupKeyAsync(cancellationToken);
                _installerAccess.Clear(HttpContext);
            }

            Response.OnCompleted(() =>
            {
                _lifetime.StopApplication();
                return Task.CompletedTask;
            });
            return View("UpgradeComplete");
        }

        private bool IsAuthorized() =>
            _installerAccess.HasAccess(HttpContext) ||
            (User.Identity?.IsAuthenticated == true &&
             (User.IsInRole(DefaultRoleManifest.SuperAdmin) ||
              // Existing installations used Admin as the upgrade authority before SuperAdmin was introduced.
              User.IsInRole(DefaultRoleManifest.Admin) ||
              User.Claims.Any(x => x.Type == "Permission" && x.Value == Permissions.SuperAdmin)));
    }
}
