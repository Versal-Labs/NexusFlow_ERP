using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class SalesController : Controller
    {
        [Authorize(Policy = Permissions.Sales.ViewInvoices)]
        public IActionResult Invoices()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Sales.ViewCreditNotes)]
        public IActionResult CreditNotes()
        {
            return View();
        }
    }
}
