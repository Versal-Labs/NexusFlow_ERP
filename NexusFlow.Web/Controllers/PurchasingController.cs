using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class PurchasingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult GRN() => View();
        public IActionResult SupplierBills() => View();
        public IActionResult DebitNotes()
        {
            return View();
        }
    }
}
