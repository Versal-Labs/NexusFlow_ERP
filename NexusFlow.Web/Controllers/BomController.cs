using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
    public class BomController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
