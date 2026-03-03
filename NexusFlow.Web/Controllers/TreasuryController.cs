using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class TreasuryController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Receipts() => View();
    }
}
