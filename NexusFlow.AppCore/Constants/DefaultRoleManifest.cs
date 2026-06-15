using System.Reflection;

namespace NexusFlow.AppCore.Constants
{
    public static class DefaultRoleManifest
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string Accountant = "Accountant";
        public const string StoreKeeper = "StoreKeeper";
        public const string SalesRep = "SalesRep";

        public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Roles { get; } =
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [SuperAdmin] = [Permissions.SuperAdmin],
                [Admin] = AllPermissions(),
                [Accountant] =
                [
                    Permissions.Finance.ViewChartOfAccounts, Permissions.Finance.ManageAccounts,
                    Permissions.Finance.ViewJournals, Permissions.Finance.PostJournal,
                    Permissions.Finance.ManagePeriods, Permissions.Finance.BankReconciliation,
                    Permissions.Finance.ViewReports, Permissions.Treasury.ViewReceipts,
                    Permissions.Treasury.CreateReceipt, Permissions.Treasury.ViewPayments,
                    Permissions.Treasury.CreatePayment, Permissions.Treasury.ManageCheques,
                    Permissions.Purchasing.ViewPOs, Permissions.Purchasing.ViewGRNs,
                    Permissions.Purchasing.ViewBills, Permissions.Purchasing.ViewDebitNotes,
                    Permissions.Sales.ViewOrders, Permissions.Sales.ViewInvoices,
                    Permissions.Sales.ViewCreditNotes, Permissions.MasterData.ViewCustomers,
                    Permissions.MasterData.ViewSuppliers, Permissions.MasterData.ViewProducts,
                    Permissions.Reporting.ViewSalesRegister, Permissions.Reporting.ViewArAging,
                    Permissions.Reporting.ViewApAging, Permissions.Reporting.ViewPurchaseRegister,
                    Permissions.Reporting.ViewInventoryValuation, Permissions.Reporting.ViewFinancialStatements,
                    Permissions.Reporting.ViewCustomerStatement, Permissions.Reporting.ViewSupplierStatement,
                    Permissions.Reporting.ViewInventoryAnalytics, Permissions.Reporting.ViewChequeVaultAnalytics,
                    Permissions.Reporting.ViewGeneralLedger, Permissions.Reporting.ViewCommissionControl
                ],
                [StoreKeeper] =
                [
                    Permissions.Inventory.ViewStock, Permissions.Inventory.TransferStock,
                    Permissions.Inventory.AdjustStock, Permissions.Inventory.ViewStockTakes,
                    Permissions.Inventory.InitiateStockTake, Permissions.Inventory.SubmitCount,
                    Permissions.Inventory.ApproveStockTake, Permissions.Inventory.RunProduction,
                    Permissions.MasterData.ViewProducts, Permissions.MasterData.ManageProducts,
                    Permissions.MasterData.ManageWarehouses, Permissions.MasterData.ManageBOMs,
                    Permissions.Purchasing.ViewPOs, Permissions.Purchasing.ViewGRNs,
                    Permissions.Purchasing.CreateGRN, Permissions.Reporting.ViewInventoryAnalytics
                ],
                [SalesRep] =
                [
                    Permissions.Sales.ViewOrders, Permissions.Sales.CreateOrder,
                    Permissions.Sales.ViewInvoices, Permissions.Sales.CreateInvoice,
                    Permissions.MasterData.ViewCustomers, Permissions.MasterData.ManageCustomers,
                    Permissions.MasterData.ViewProducts, Permissions.Reporting.ViewRepCommissions,
                    Permissions.HR.ViewCommissionsOwn
                ]
            };

        private static IReadOnlyCollection<string> AllPermissions()
        {
            return typeof(Permissions)
                .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
                .Select(field => field.GetValue(null)?.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
