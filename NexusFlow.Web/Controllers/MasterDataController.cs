using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.HybridScheme)]
    public class MasterDataController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("MasterData/Brands")]
        public IActionResult Brands()
        {
            return View();
        }

        [HttpGet("MasterData/Definitions")]
        public IActionResult Definitions()
        {
            return View();
        }
    }
}
