using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class FinancialPeriodController : Controller
    {
        [Authorize(Policy = Permissions.Finance.ManagePeriods)]
        public IActionResult Index()
        {
            return View();
        }
    }
}
