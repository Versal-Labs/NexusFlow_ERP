using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
    public class SalesOrderController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/Sales/Orders.cshtml");
        }
    }
}
