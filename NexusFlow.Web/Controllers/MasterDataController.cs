using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class MasterDataController : Controller
    {
        [Authorize(Policy = Permissions.MasterData.ManageProducts)]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("MasterData/Brands")]
        [Authorize(Policy = Permissions.MasterData.ManageProducts)]
        public IActionResult Brands()
        {
            return View();
        }

        [HttpGet("MasterData/Definitions")]
        [Authorize(Policy = Permissions.MasterData.ManageProducts)]
        public IActionResult Definitions()
        {
            return View();
        }

        [Authorize(Policy = Permissions.MasterData.ManageWarehouses)]
        public IActionResult Warehouses() => View();
    }
}
