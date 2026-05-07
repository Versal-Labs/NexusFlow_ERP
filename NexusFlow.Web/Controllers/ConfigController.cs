using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class ConfigController : Controller
    {
        [Authorize(Policy = Permissions.System.ManageConfigs)]
        public IActionResult Index()
        {
            return View();
        }
    }
}
