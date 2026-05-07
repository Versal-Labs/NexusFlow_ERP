using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class TreasuryController : Controller
    {
        [Authorize(Policy = Permissions.Treasury.ViewReceipts)]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Treasury.ViewReceipts)]
        public IActionResult Receipts() => View();

        [Authorize(Policy = Permissions.Treasury.ViewPayments)]
        public IActionResult Payments() => View();
    }
}
