using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class BillOfMaterialController : Controller
    {
        [Authorize(Policy = Permissions.MasterData.ManageBOMs)]
        public IActionResult Index()
        {
            return View("~/Views/MasterData/BillOfMaterials.cshtml");
        }
    }
}
