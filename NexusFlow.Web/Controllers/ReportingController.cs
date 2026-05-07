using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusFlow.AppCore.Constants;

namespace NexusFlow.Web.Controllers
{
    [Authorize(AuthenticationSchemes = AuthConstants.IdentityScheme)]
    public class ReportingController : Controller
    {
        [Authorize(Policy = Permissions.Reporting.ViewSalesRegister)]
        public IActionResult SalesRegister()
        {
            return View();
        }

        [Authorize(Policy = Permissions.Reporting.ViewArAging)]
        public IActionResult ArAging() => View();

        [Authorize(Policy = Permissions.Reporting.ViewApAging)]
        public IActionResult ApAging() => View();

        [Authorize(Policy = Permissions.Reporting.ViewCustomerStatement)]
        public IActionResult CustomerStatement() => View();

        [Authorize(Policy = Permissions.Reporting.ViewSupplierStatement)]
        public IActionResult SupplierStatement() => View();

        [Authorize(Policy = Permissions.Reporting.ViewInventoryAnalytics)]
        public IActionResult InventoryAnalytics() => View();

        [Authorize(Policy = Permissions.Reporting.ViewChequeVaultAnalytics)]
        public IActionResult ChequeVaultAnalytics() => View();

        [Authorize(Policy = Permissions.Reporting.ViewGeneralLedger)]
        public IActionResult GeneralLedger() => View();

        [Authorize(Policy = Permissions.Reporting.ViewCommissionControl)]
        public IActionResult CommissionControl() => View();

        [Authorize(Policy = Permissions.Reporting.ViewRepCommissions)]
        public IActionResult RepPortal() => View();
    }
}
