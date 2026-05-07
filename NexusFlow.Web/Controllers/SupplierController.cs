using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class SupplierController : Controller
    {
        [HttpGet]
        [Authorize(Policy = Permissions.MasterData.ViewSuppliers)]
        public IActionResult Index()
        {
            return View();
        }
    }
}
