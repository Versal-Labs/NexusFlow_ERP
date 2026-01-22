using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.HybridScheme)]
    public class ConfigController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
