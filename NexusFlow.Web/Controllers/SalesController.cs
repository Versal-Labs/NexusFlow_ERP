using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class SalesController : Controller
    {
        public IActionResult Invoices()
        {
            return View();
        }

        public IActionResult CreditNotes()
        {
            return View();
        }
    }
}
