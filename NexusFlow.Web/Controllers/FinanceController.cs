using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class FinanceController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult TrialBalance() => View();

        public IActionResult JournalAudit()
        {
            return View();
        }

        public IActionResult ChequeVault()
        {
            return View();
        }
        public IActionResult BankReconciliation()
        {
            return View();
        }

        public IActionResult ProfitAndLoss()
        {
            return View();
        }

        public IActionResult BalanceSheet()
        {
            return View();
        }

        public IActionResult ImportOpenInvoices()
        {
            return View();
        }

        public IActionResult ImportTrialBalance()
        {
            return View();
        }
    }
}
