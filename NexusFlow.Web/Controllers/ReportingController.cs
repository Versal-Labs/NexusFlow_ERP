using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class ReportingController : Controller
    {
        public IActionResult SalesRegister()
        {
            return View();
        }

        public IActionResult ArAging() => View();
        public IActionResult ApAging() => View();
        public IActionResult CustomerStatement() => View();
        public IActionResult SupplierStatement() => View();
        public IActionResult InventoryAnalytics() => View();
        public IActionResult ChequeVaultAnalytics() => View();
        public IActionResult GeneralLedger() => View();
    }
}
