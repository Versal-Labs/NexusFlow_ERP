using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers;

[Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
[Authorize(Policy = Permissions.Migration.View)]
public sealed class MigrationController : Controller
{
    public IActionResult Index() => View();
}
