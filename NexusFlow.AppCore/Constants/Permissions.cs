using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Constants
{
    public static class Permissions
    {
        // A special permission that bypasses all checks
        // The master key. Anyone with this claim bypasses all checks.
        public const string SuperAdmin = "Permissions.SuperAdmin";

        public static class Sales
        {
            public const string ViewOrders = "Permissions.Sales.ViewOrders";
            public const string CreateOrder = "Permissions.Sales.CreateOrder";
            public const string ViewInvoices = "Permissions.Sales.ViewInvoices";
            public const string CreateInvoice = "Permissions.Sales.CreateInvoice";
            public const string VoidInvoice = "Permissions.Sales.VoidInvoice";
            public const string ViewCreditNotes = "Permissions.Sales.ViewCreditNotes";
            public const string CreateCreditNote = "Permissions.Sales.CreateCreditNote";
        }

        public static class Purchasing
        {
            public const string ViewPOs = "Permissions.Purchasing.ViewPOs";
            public const string CreatePO = "Permissions.Purchasing.CreatePO";
            public const string ApprovePO = "Permissions.Purchasing.ApprovePO";
            public const string ViewGRNs = "Permissions.Purchasing.ViewGRNs";
            public const string CreateGRN = "Permissions.Purchasing.CreateGRN";
            public const string ViewBills = "Permissions.Purchasing.ViewBills";
            public const string CreateBill = "Permissions.Purchasing.CreateBill";
            public const string ViewDebitNotes = "Permissions.Purchasing.ViewDebitNotes";
        }

        public static class Inventory
        {
            public const string ViewStock = "Permissions.Inventory.ViewStock";
            public const string TransferStock = "Permissions.Inventory.TransferStock";
            public const string AdjustStock = "Permissions.Inventory.AdjustStock";
            public const string ViewStockTakes = "Permissions.Inventory.ViewStockTakes";
            public const string InitiateStockTake = "Permissions.Inventory.InitiateStockTake";
            public const string SubmitCount = "Permissions.Inventory.SubmitCount";
            public const string ApproveStockTake = "Permissions.Inventory.ApproveStockTake";
            public const string RunProduction = "Permissions.Inventory.RunProduction";
        }

        public static class Treasury
        {
            public const string ViewReceipts = "Permissions.Treasury.ViewReceipts";
            public const string CreateReceipt = "Permissions.Treasury.CreateReceipt";
            public const string VoidReceipt = "Permissions.Treasury.VoidReceipt";
            public const string ViewPayments = "Permissions.Treasury.ViewPayments";
            public const string CreatePayment = "Permissions.Treasury.CreatePayment";
            public const string ManageCheques = "Permissions.Treasury.ManageCheques";
        }

        public static class Finance
        {
            public const string ViewChartOfAccounts = "Permissions.Finance.ViewChartOfAccounts";
            public const string ManageAccounts = "Permissions.Finance.ManageAccounts";
            public const string ViewJournals = "Permissions.Finance.ViewJournals";
            public const string PostJournal = "Permissions.Finance.PostJournal";
            public const string ManagePeriods = "Permissions.Finance.ManagePeriods";
            public const string BankReconciliation = "Permissions.Finance.BankReconciliation";
            public const string ViewReports = "Permissions.Finance.ViewReports"; // Trial Balance, P&L, etc.
        }

        public static class MasterData
        {
            public const string ViewCustomers = "Permissions.MasterData.ViewCustomers";
            public const string ManageCustomers = "Permissions.MasterData.ManageCustomers";
            public const string ViewSuppliers = "Permissions.MasterData.ViewSuppliers";
            public const string ManageSuppliers = "Permissions.MasterData.ManageSuppliers";
            public const string ViewProducts = "Permissions.MasterData.ViewProducts";
            public const string ManageProducts = "Permissions.MasterData.ManageProducts";
            public const string ManageBOMs = "Permissions.MasterData.ManageBOMs";
            public const string ManageWarehouses = "Permissions.MasterData.ManageWarehouses";
        }

        public static class HR
        {
            public const string ViewEmployees = "Permissions.HR.ViewEmployees";
            public const string ManageEmployees = "Permissions.HR.ManageEmployees";
            public const string ManageCommissionRules = "Permissions.HR.ManageCommissionRules";
            public const string ViewCommissionsOwn = "Permissions.HR.ViewCommissionsOwn"; // Row-Level Security!
            public const string ViewCommissionsAll = "Permissions.HR.ViewCommissionsAll";
        }

        public static class System
        {
            public const string ViewUsers = "Permissions.System.ViewUsers";
            public const string ManageUsers = "Permissions.System.ManageUsers";
            public const string ManageRoles = "Permissions.System.ManageRoles";
            public const string ManageConfigs = "Permissions.System.ManageConfigs";
            public const string ViewAuditLogs = "Permissions.System.ViewAuditLogs";
        }

        public static class Reporting
        {
            public const string ViewSalesRegister = "Permissions.Reporting.ViewSalesRegister";
            public const string ViewArAging = "Permissions.Reporting.ViewArAging";
            public const string ViewApAging = "Permissions.Reporting.ViewApAging";
            public const string ViewPurchaseRegister = "Permissions.Reporting.ViewPurchaseRegister";
            public const string ViewInventoryValuation = "Permissions.Reporting.ViewInventoryValuation";
            public const string ViewFinancialStatements = "Permissions.Reporting.ViewFinancialStatements";
            public const string ViewCustomerStatement = "Permissions.Reporting.ViewCustomerStatement";
            public const string ViewSupplierStatement = "Permissions.Reporting.ViewSupplierStatement";
            public const string ViewInventoryAnalytics = "Permissions.Reporting.ViewInventoryAnalytics";
            public const string ViewChequeVaultAnalytics = "Permissions.Reporting.ViewChequeVaultAnalytics";
            public const string ViewGeneralLedger = "Permissions.Reporting.ViewGeneralLedger";
        }
    }
}
