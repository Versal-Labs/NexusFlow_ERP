using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
    public class CommissionRuleController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/Sales/CommissionRules.cshtml");
        }
    }
}
