using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
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
