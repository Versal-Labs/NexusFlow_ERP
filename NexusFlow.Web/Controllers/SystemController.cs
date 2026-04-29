using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class SystemController : Controller
    {
        public IActionResult Users()
        {
            // Returns ~/Views/System/Users.cshtml
            return View();
        }

        [HttpGet]
        public IActionResult AuditExplorer()
        {
            // Returns the HTML Shell for the SPA-like DataTable experience
            return View();
        }
    }
}
