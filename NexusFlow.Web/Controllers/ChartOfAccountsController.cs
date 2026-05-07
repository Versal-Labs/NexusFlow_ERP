using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class ChartOfAccountsController : Controller
    {
        [Authorize(Policy = Permissions.Finance.ViewChartOfAccounts)]
        public IActionResult Index()
        {
            return View();
        }
    }
}
