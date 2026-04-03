using Microsoft.AspNetCore.Mvc;

namespace NexusFlow.Web.Controllers
{
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
    }
}
