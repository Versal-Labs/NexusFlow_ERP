using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
    public class StockTakeController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/Inventory/StockTake.cshtml");
        }
    }
}
