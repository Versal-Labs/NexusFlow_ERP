using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
    public class BillOfMaterialController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/MasterData/BillOfMaterials.cshtml");
        }
    }
}
