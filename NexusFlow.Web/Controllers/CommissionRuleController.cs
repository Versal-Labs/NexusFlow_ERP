using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class CommissionRuleController : Controller
    {
        [Authorize(Policy = Permissions.HR.ManageCommissionRules)]
        public IActionResult Index()
        {
            return View("~/Views/Sales/CommissionRules.cshtml");
        }
    }
}
