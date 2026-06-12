using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
    public class PayrollController : Controller
    {
        public IActionResult PayrollProcessing()
        {
            return View();
        }

        public IActionResult AttendanceDashboard()
        {
            return View();
        }

        public IActionResult PayrollConfig()
        {
            return View();
        }
    }
}
