using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class EmployeeController : Controller
    {
        [Authorize(Policy = Permissions.HR.ViewEmployees)]
        public IActionResult Index()
        {
            return View("~/Views/HR/Employees.cshtml");
        }
    }
}
