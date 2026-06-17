using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class InventoryController : Controller
    {
        // GET: /Inventory 
        // (This is the main Stock Valuation Dashboard we will build later)
        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(StockValuation));
        }

        // GET: /Inventory/MaterialIssue 
        // (Serves the Phase 2 UI)
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public IActionResult MaterialIssue()
        {
            return View();
        }

        // GET: /Inventory/ProductionReceipt 
        // (Serves the Phase 3 UI)
        [Authorize(Policy = Permissions.Inventory.RunProduction)]
        public IActionResult ProductionReceipt()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Inventory.ViewStock)]
        public IActionResult StockValuation()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Inventory.TransferStock)]
        public IActionResult Transfers()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Inventory.AdjustStock)]
        public IActionResult Adjustments()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Inventory.PrintBarcodes)]
        public IActionResult BarcodeCenter()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Inventory.ManageBarcodeTemplates)]
        public IActionResult BarcodeTemplates()
        {
            return View();
        }
    }
}
