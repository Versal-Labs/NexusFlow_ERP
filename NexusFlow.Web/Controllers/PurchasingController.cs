using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class PurchasingController : Controller
    {
        [Authorize(Policy = Permissions.Purchasing.ViewPOs)]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Purchasing.ViewGRNs)]
        public IActionResult GRN() => View();

        [Authorize(Policy = Permissions.Purchasing.ViewBills)]
        public IActionResult SupplierBills() => View();

        [Authorize(Policy = Permissions.Purchasing.ViewDebitNotes)]
        public IActionResult DebitNotes()
        {
            return View();
        }
    }
}
