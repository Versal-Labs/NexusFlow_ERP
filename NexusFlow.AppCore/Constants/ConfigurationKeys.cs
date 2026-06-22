namespace NexusFlow.AppCore.Constants
{
    public static class ConfigurationKeys
    {
        public const string CompanyName = "Company.Name";
        public const string CompanyTaxRegistrationNumber = "Company.TaxRegNo";
        public const string CompanyCanonicalUrl = "Company.CanonicalUrl";
        public const string CompanyTimeZone = "Company.TimeZone";
        public const string FinanceBaseCurrency = "Finance.BaseCurrency";
        public const string InventoryAllowNegativeStock = "Inventory.AllowNegativeStock";
        public const string StorageLocalPath = "Storage.LocalPath";
        public const string ProductionOverproductionTolerancePercent = "Production.OverproductionTolerancePercent";

        public static readonly IReadOnlyList<string> Required =
        [
            CompanyName,
            CompanyTaxRegistrationNumber,
            CompanyCanonicalUrl,
            CompanyTimeZone,
            FinanceBaseCurrency,
            InventoryAllowNegativeStock,
            StorageLocalPath,
            ProductionOverproductionTolerancePercent
        ];
    }

    public static class AccountMappingKeys
    {
        public const string AccountsReceivable = "Account.Asset.AccountsReceivable";
        public const string AccountsPayable = "Account.Liability.AccountsPayable";
        public const string UndepositedFunds = "Account.Asset.UndepositedFunds";
        public const string OpeningBalanceEquity = "Account.Equity.OpeningBalance";
        public const string SalesRevenue = "Account.Sales.Revenue";
        public const string VatPayable = "Account.Tax.VATPayable";
        public const string VatReceivable = "Account.Tax.VATReceivable";
        public const string CostOfGoodsSold = "Account.Cost.COGS";
        public const string RawMaterialInventory = "Account.Inventory.RawMaterial";
        public const string FinishedGoodInventory = "Account.Inventory.FinishedGood";
        public const string WorkInProgressInventory = "Account.Inventory.WorkInProgress";
        public const string InventoryShrinkage = "Account.Inventory.Shrinkage";
        public const string InventorySurplus = "Account.Inventory.Surplus";
        public const string ServiceAccrual = "Account.Liability.ServiceAccrual";
        public const string UnbilledReceipts = "Account.Purchasing.UnbilledReceipts";
        public const string PurchaseVariance = "Account.Expense.PurchaseVariance";
        public const string BankFees = "Account.Expense.BankFees";
        public const string InterestIncome = "Account.Revenue.InterestIncome";
        public const string BasicSalaryExpense = "Account.Payroll.BasicSalaryExpense";
        public const string EmployerEpfExpense = "Account.Payroll.EmployerEPFExpense";
        public const string EmployerEtfExpense = "Account.Payroll.EmployerETFExpense";
        public const string EpfPayable = "Account.Payroll.EPFPayable";
        public const string EtfPayable = "Account.Payroll.ETFPayable";
        public const string NetSalariesPayable = "Account.Payroll.NetSalariesPayable";
        public const string EmployeeLoansReceivable = "Account.Payroll.EmployeeLoansReceivable";
        public const string SalaryAdvancesReceivable = "Account.Payroll.SalaryAdvancesReceivable";
        public const string CommissionExpense = "Account.Payroll.CommissionExpense";
        public const string AllowanceExpense = "Account.Payroll.AllowanceExpense";

        public static readonly IReadOnlyList<string> Required =
        [
            AccountsReceivable, AccountsPayable, UndepositedFunds, OpeningBalanceEquity, SalesRevenue,
            VatPayable, VatReceivable, CostOfGoodsSold, RawMaterialInventory, FinishedGoodInventory,
            WorkInProgressInventory, InventoryShrinkage, InventorySurplus, ServiceAccrual,
            UnbilledReceipts, PurchaseVariance, BankFees, InterestIncome, BasicSalaryExpense,
            EmployerEpfExpense, EmployerEtfExpense, EpfPayable, EtfPayable, NetSalariesPayable,
            EmployeeLoansReceivable, SalaryAdvancesReceivable, CommissionExpense, AllowanceExpense
        ];
    }

    public static class NumberSequenceKeys
    {
        public const string CreditNote = "CreditNote";
        public const string DebitNote = "DebitNote";
        public const string Employee = "EMP";
        public const string GoodsReceipt = "GRN";
        public const string Journal = "JOURNAL";
        public const string MaterialIssue = "MaterialIssue";
        public const string OpeningStock = "OpeningStock";
        public const string SalesOrder = "ORD";
        public const string Payment = "Payment";
        public const string ProductionReceipt = "ProductionReceipt";
        public const string Purchasing = "Purchasing";
        public const string Receipt = "Receipt";
        public const string SalesInvoice = "SalesInvoice";
        public const string StockAdjustment = "StockAdjustment";
        public const string StockTake = "StockTake";
        public const string StockTransfer = "StockTransfer";
        public const string SupplierBill = "SupplierBill";
        public const string CustomerDebitMemo = "CustomerDebitMemo";
        public const string ProductionOrder = "ProductionOrder";
        public const string MaterialReturn = "MaterialReturn";

        public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CreditNote] = "CN", [DebitNote] = "DN", [Employee] = "EMP", [GoodsReceipt] = "GRN",
            [Journal] = "JE", [MaterialIssue] = "MI", [OpeningStock] = "OBSTK", [SalesOrder] = "SO",
            [Payment] = "PAY", [ProductionReceipt] = "PRD", [Purchasing] = "PO", [Receipt] = "REC",
            [SalesInvoice] = "INV", [StockAdjustment] = "ADJ", [StockTake] = "ST", [StockTransfer] = "TRF",
            [SupplierBill] = "BILL", [CustomerDebitMemo] = "CDM", [ProductionOrder] = "PWO",
            [MaterialReturn] = "MR"
        };

        public static IReadOnlyList<string> Required => Defaults.Keys.ToArray();
    }
}
