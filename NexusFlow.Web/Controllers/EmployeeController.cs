using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
    public class EmployeeController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/HR/Employees.cshtml");
        }
    }
}
