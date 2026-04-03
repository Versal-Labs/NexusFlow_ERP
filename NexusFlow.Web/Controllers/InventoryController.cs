using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
    public class InventoryController : Controller
    {
        // GET: /Inventory 
        // (This is the main Stock Valuation Dashboard we will build later)
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Inventory/MaterialIssue 
        // (Serves the Phase 2 UI)
        public IActionResult MaterialIssue()
        {
            return View();
        }

        // GET: /Inventory/ProductionReceipt 
        // (Serves the Phase 3 UI)
        public IActionResult ProductionReceipt()
        {
            return View();
        }
    }
}
