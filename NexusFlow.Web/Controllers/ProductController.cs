using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;
using NexusFlow.Web.Models.Products;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class ProductController : Controller
    {
        [Authorize(Policy = Permissions.MasterData.ViewProducts)]
        public IActionResult Index()
        {
            return View();
        }
    }
}
