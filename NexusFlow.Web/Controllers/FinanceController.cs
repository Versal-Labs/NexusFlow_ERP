using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class FinanceController : Controller
    {
        [Authorize(Policy = Permissions.Finance.ViewChartOfAccounts)]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(TrialBalance));
        }

        [Authorize(Policy = Permissions.Finance.ViewReports)]
        public IActionResult TrialBalance() => View();

        [Authorize(Policy = Permissions.Finance.ViewJournals)]
        public IActionResult JournalAudit()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Treasury.ManageCheques)]
        public IActionResult ChequeVault()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Finance.BankReconciliation)]
        public IActionResult BankReconciliation()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Finance.ViewReports)]
        public IActionResult ProfitAndLoss()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Finance.ViewReports)]
        public IActionResult BalanceSheet()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Finance.ManageAccounts)]
        public IActionResult ImportOpenInvoices()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Finance.ManageAccounts)]
        public IActionResult ImportTrialBalance()
        {
            return View();
        }
    }
}
