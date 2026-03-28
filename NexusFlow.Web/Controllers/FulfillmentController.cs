using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class FulfillmentController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/Sales/Fulfillment.cshtml");
        }
    }
}
