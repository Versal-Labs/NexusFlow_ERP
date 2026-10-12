using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NexusFlow.AppCore.Features.Migration;

public static class MigrationJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Code(string value) => value.Trim().ToUpperInvariant();
}

public sealed class MigrationRow
{
    public int Row { get; set; }
    public Dictionary<string, string> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Raw { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Get(string key) => Data.TryGetValue(key, out var value) ? value.Trim() : "";
    public void Set(string key, string value) => Data[key] = value;
    public decimal Decimal(string key) => decimal.Parse(Get(key), NumberStyles.Number, CultureInfo.InvariantCulture);
    public DateTime Date(string key) => DateTime.ParseExact(Get(key), "yyyy-MM-dd", CultureInfo.InvariantCulture);
    public bool Flag(string key) => Get(key).Equals("true", StringComparison.OrdinalIgnoreCase);
    // Source row number/file hash are deliberately absent from record identity and content.
    public string Canonical => MigrationJson.Write(Data.Where(x => !x.Key.StartsWith('_')).OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value));
}
public sealed record MigrationReadResult(List<MigrationRow> Rows, List<string> Errors, DateTime? ReportDate = null);
public interface IMigrationFileReader
{
    MigrationReadResult Read(string module, string filename, byte[] bytes);
}
public sealed record MigrationCreateRequest(string Name, string SourceSystem, DateTime OpeningDate, string? AdoptionEvidence = null);
public sealed class MigrationSettings
{
    public int OpeningEquityAccountId { get; set; }
    public Dictionary<string, Dictionary<string, int>> Mappings { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> Columns { get; set; } = new();
    public Dictionary<string, string> DatasetStates { get; set; } = MigrationCatalog.Modules.ToDictionary(x => x.Key, _ => "AwaitingData");
    public Dictionary<string, string> ZeroEvidence { get; set; } = new();
    public string SummarizedStockEvidence { get; set; } = "";
    public string FinishedGoodsValuation { get; set; } = "";
    public string FinishedGoodsValuationEvidence { get; set; } = "";
    public int? SupplierTermDays { get; set; }
    public Dictionary<string, int> Defaults { get; set; } = new();
}
public sealed class MigrationSelection
{
    public List<string> Modules { get; set; } = [];
    public List<string> Customers { get; set; } = [];
    public List<string> Suppliers { get; set; } = [];
    public List<string> Skus { get; set; } = [];
    public List<string> Banks { get; set; } = [];
}
public sealed record MigrationIssue(string Module, string Scope, int Row, string Message);
public sealed record MigrationLine(int AccountId, decimal Debit, decimal Credit);
public sealed class MigrationPreviewUnit
{
    public string Module { get; set; } = "";
    public string Scope { get; set; } = "";
    public List<MigrationRow> Rows { get; set; } = [];
    public List<MigrationLine> Journal { get; set; } = [];
    public List<MigrationIssue> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string Hash { get; set; } = "";
    public string Status { get; set; } = "Pending";
}
public sealed class MigrationPreview
{
    public int ProjectId { get; set; }
    public int Version { get; set; }
    public string Hash { get; set; } = "";
    public List<MigrationPreviewUnit> Units { get; set; } = [];
    public List<MigrationIssue> Errors { get; set; } = [];
    public bool CanApprove => Errors.Count == 0 && Units.Any(x => x.Status == "Pending") && Units.All(x => x.Errors.Count == 0);
}
public sealed record MigrationApproveRequest(MigrationSelection Selection, string Hash, string Evidence);
public sealed record MigrationScopeRequest(string Kind, string Code, List<string> RequiredModules, string Evidence, bool NoLegacyOpening = false);
public sealed record MigrationReleaseRequest(string Evidence, Dictionary<string, decimal> Controls, Dictionary<string, int> RowCounts);
public sealed record MigrationReversalRequest(DateTime Date, string Reason);
public sealed record MigrationBulkReleaseRequest(MigrationSelection Selection, string Evidence);

public static class MigrationCatalog
{
    public const string Version = "1.0";
    public static readonly Dictionary<string, string[]> Modules = new()
    {
        ["Accounts"] = ["LegacyRecordId", "Code", "Name", "AccountType", "ParentCode"],
        ["CustomerMaster"] = ["LegacyRecordId", "Code", "Name", "Contact", "Email", "Phone", "Mobile", "Address1", "Address2", "TaxNumber", "GroupCode", "PaymentTermCode", "PriceLevelCode", "ReceivableAccountCode", "RevenueAccountCode"],
        ["SupplierMaster"] = ["LegacyRecordId", "Code", "Name", "Contact", "Email", "Phone", "Mobile", "Address1", "Address2", "TaxNumber", "GroupCode", "PaymentTermCode", "PayableAccountCode", "ExpenseAccountCode"],
        ["CustomerOpenings"] = ["LegacyRecordId", "Code", "Reference", "Kind", "DocumentDate", "DueDate", "Amount"],
        ["SupplierOpenings"] = ["LegacyRecordId", "Code", "Reference", "Kind", "DocumentDate", "DueDate", "Amount"],
        ["ReceivedCheques"] = ["LegacyRecordId", "Code", "Reference", "DocumentReferences", "ChequeNumber", "ChequeDate", "ReceivedDate", "BankBranchCode", "Amount", "Status", "PartyReflected", "BankReflected", "BankAccountCode", "SupplierCode", "SafeAccountCode"],
        ["IssuedCheques"] = ["LegacyRecordId", "Code", "Reference", "ChequeNumber", "ChequeDate", "DocumentDate", "Amount", "BankAccountCode", "PartyReflected", "BankReflected"],
        ["Stock"] = ["LegacyRecordId", "Code", "Name", "ProductType", "BrandCode", "CategoryCode", "UnitCode", "WarehouseCode", "Quantity", "UnitCost", "LayerDate", "Batch", "ConversionFactor", "InventoryAccountCode", "SellingPrice", "Size", "Color"],
        ["BankOpening"] = ["LegacyRecordId", "Code", "Amount", "StatementBalance"],
        ["BankRecon"] = ["LegacyRecordId", "Code", "Reference", "DocumentDate", "Amount", "Kind", "BankReflected"],
        ["TrialBalance"] = ["LegacyRecordId", "Code", "Debit", "Credit"],
        ["ControlTotals"] = ["LegacyRecordId", "Kind", "Code", "Component", "Amount", "RowCount"]
    };
    public static readonly string[] FinancialModules = ["CustomerOpenings", "SupplierOpenings", "ReceivedCheques", "IssuedCheques", "Stock", "BankOpening", "BankRecon", "TrialBalance"];
    public static string ScopeKind(string module) => module switch
    {
        "CustomerMaster" or "CustomerOpenings" or "ReceivedCheques" => "Customer",
        "SupplierMaster" or "SupplierOpenings" or "IssuedCheques" => "Supplier",
        "Stock" => "Stock",
        "BankOpening" or "BankRecon" => "Bank",
        _ => "General"
    };
    public static string Identity(string module, MigrationRow row)
    {
        var id = row.Get("LegacyRecordId");
        if (id.Length != 0) return id;
        if (module == "ControlTotals") return MigrationJson.Write(new[] { row.Get("Kind"), row.Get("Code"), row.Get("Component") });
        if (module.EndsWith("Master") || module is "Accounts" or "BankOpening" or "TrialBalance") return row.Get("Code");
        if (module == "Stock") return MigrationJson.Write(new[] { row.Get("Code"), row.Get("WarehouseCode"), row.Get("Batch"), row.Get("LayerDate") });
        return MigrationJson.Write(new[] { row.Get("Code"), row.Get("Kind"), row.Get("Reference"), row.Get(module == "ReceivedCheques" ? "ChequeDate" : "DocumentDate"), row.Get("ChequeNumber") });
    }
}
