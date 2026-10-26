using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Features.Migration;

public sealed partial class MigrationCentreService
{
    public async Task<MigrationPreview> ValidateAsync(int id, MigrationSelection selection, CancellationToken ct, Func<int, int, string, int, Task>? report = null)
    {
        var p = await Project(id, false, ct); var s = MigrationJson.Read<MigrationSettings>(p.SettingsJson);
        var result = new MigrationPreview { ProjectId = id, Version = p.Version };
        if (selection.Modules.Count == 0 || selection.Modules.Distinct().Count() != selection.Modules.Count || selection.Modules.Except(MigrationCatalog.Modules.Keys).Any()) throw new InvalidOperationException("Select one or more known modules once each.");
        // Scoped checks still use fresh database state, but must not repeatedly read and parse
        // unrelated files (especially the large independent control schedule) for every SKU/party.
        var requiredDatasets = selection.Modules.ToHashSet(StringComparer.Ordinal);
        if (selection.Modules.Any(x => x is "CustomerOpenings" or "ReceivedCheques")) requiredDatasets.Add("CustomerMaster");
        if (selection.Modules.Any(x => x is "SupplierOpenings" or "IssuedCheques" or "ReceivedCheques")) requiredDatasets.Add("SupplierMaster");
        if (selection.Modules.Contains("BankRecon")) requiredDatasets.UnionWith(["ReceivedCheques", "IssuedCheques"]);
        var datasetModules = requiredDatasets.ToArray();
        var datasets = await db.MigrationDatasets.AsNoTracking().Where(x => x.ProjectId == id && x.IsCurrent && datasetModules.Contains(x.Module)).ToListAsync(ct);
        var origins = await db.MigrationOrigins.AsNoTracking().Where(x => x.SourceSystem == p.SourceSystem && x.IsCurrent && selection.Modules.Contains(x.RecordKind))
            .Select(x => new { x.RecordKind, x.IdentityHash, x.ContentHash }).ToListAsync(ct);
        var originsByIdentity = origins.ToDictionary(x => (x.RecordKind, x.IdentityHash));
        var needsLedger = selection.Modules.Contains("TrialBalance");
        var posted = await db.MigrationUnits.AsNoTracking().Where(x => x.ProjectId == id && (needsLedger || selection.Modules.Contains(x.Module)))
            .Select(x => new MigrationUnit { Id = x.Id, Module = x.Module, ScopeKey = x.ScopeKey, Hash = x.Hash, Status = x.Status,
                JournalEntryId = x.JournalEntryId, RowsJson = needsLedger ? x.RowsJson : "[]" }).ToListAsync(ct);
        var projectScopes = await db.MigrationScopes.AsNoTracking().Where(x => x.ProjectId == id)
            .Select(x => new { x.Kind, x.Code, x.Status }).ToListAsync(ct);
        var accounts = await db.Accounts.AsNoTracking().ToListAsync(ct);
        var customers = await db.Customers.AsNoTracking().ToListAsync(ct);
        var suppliers = await db.Suppliers.AsNoTracking().ToListAsync(ct);
        var variants = await db.ProductVariants.AsNoTracking().Include(x => x.Product).ThenInclude(x => x.Category).ToListAsync(ct);
        var warehouses = await db.Warehouses.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().ToListAsync(ct);
        var managedAccounts = customers.Select(x => x.DefaultReceivableAccountId).Concat(suppliers.Where(x => x.DefaultPayableAccountId != null).Select(x => x.DefaultPayableAccountId!.Value)).Concat(categories.Where(x => x.InventoryAccountId != null).Select(x => x.InventoryAccountId!.Value)).Concat(warehouses.Where(x => x.OverrideInventoryAccountId != null).Select(x => x.OverrideInventoryAccountId!.Value)).Concat(accounts.Where(x => x.RequiresReconciliation).Select(x => x.Id)).ToHashSet();
        if (s.Defaults.TryGetValue("SafeAccount", out var safeAccount)) managedAccounts.Add(safeAccount);
        var safeCode = await db.SystemConfigs.Where(x => x.Key == "Account.Asset.UndepositedFunds").Select(x => x.Value).SingleOrDefaultAsync(ct);
        var configuredSafe = accounts.SingleOrDefault(x => x.Code == safeCode); if (configuredSafe != null) managedAccounts.Add(configuredSafe.Id);
        var lookup = new Dictionary<string, HashSet<int>>
        {
            ["Account"] = accounts.Where(x => x.IsActive && x.IsTransactionAccount).Select(x => x.Id).ToHashSet(),
            ["Warehouse"] = warehouses.Select(x => x.Id).ToHashSet(),
            ["Unit"] = (await db.UnitOfMeasures.Select(x => x.Id).ToListAsync(ct)).ToHashSet(),
            ["Brand"] = (await db.Brands.Select(x => x.Id).ToListAsync(ct)).ToHashSet(),
            ["Category"] = (await db.Categories.Select(x => x.Id).ToListAsync(ct)).ToHashSet(),
            ["BankBranch"] = (await db.BankBranches.Select(x => x.Id).ToListAsync(ct)).ToHashSet(),
            ["Customer"] = customers.Select(x => x.Id).ToHashSet(), ["Supplier"] = suppliers.Select(x => x.Id).ToHashSet(), ["Stock"] = variants.Select(x => x.Id).ToHashSet()
        };
        foreach (var group in (await db.SystemLookups.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct)).GroupBy(x => x.Type)) lookup[group.Key] = group.Select(x => x.Id).ToHashSet();
        int Resolve(string type, string code, string defaultKey = "")
        {
            var target = 0;
            if (s.Mappings.TryGetValue(type, out var map)) map.TryGetValue(MigrationJson.Code(code), out target);
            if (target == 0 && code == "" && defaultKey != "") s.Defaults.TryGetValue(defaultKey, out target);
            if (type == "Account" && target == 0 && code != "") target = accounts.SingleOrDefault(x => x.Code == code)?.Id ?? 0;
            return lookup.TryGetValue(type, out var ids) && ids.Contains(target) ? target : 0;
        }
        bool Selected(string kind, string code) { var list = kind switch { "Customer" => selection.Customers, "Supplier" => selection.Suppliers, "Stock" => selection.Skus, "Bank" => selection.Banks, _ => [] }; return list.Count == 0 || list.Select(MigrationJson.Code).Contains(code); }
        var allRows = new Dictionary<string, List<MigrationRow>>();
        foreach (var dataset in datasets)
        {
            var rows = MigrationJson.Read<List<MigrationRow>>(dataset.RowsJson);
            foreach (var row in rows)
            {
                row.Data = new(row.Data, StringComparer.OrdinalIgnoreCase); row.Raw = new(row.Raw, StringComparer.OrdinalIgnoreCase);
                foreach (var field in MigrationCatalog.Modules[dataset.Module]) if (!row.Data.ContainsKey(field)) row.Set(field, "");
                if (s.Columns.TryGetValue(dataset.Module, out var columns)) foreach (var c in columns) row.Set(c.Key, row.Raw.TryGetValue(c.Value, out var value) ? value : "");
                row.Set("Code", MigrationJson.Code(row.Get("Code")));
                if (dataset.Module == "Stock" && row.Get("CandidateUnitCost") != "" && s.FinishedGoodsValuation == "AverageCost" && s.FinishedGoodsValuationEvidence.Trim() != "") row.Set("UnitCost", row.Get("CandidateUnitCost"));
            }
            allRows[dataset.Module] = rows;
        }
        var totalUnits = allRows.Where(x => selection.Modules.Contains(x.Key) && s.DatasetStates.GetValueOrDefault(x.Key) != "NotApplicable")
            .Sum(x => x.Value.Where(r => Selected(MigrationCatalog.ScopeKind(x.Key), r.Get("Code"))).Select(r => MigrationCatalog.ScopeKind(x.Key) == "General" ? "ALL" : r.Get("Code")).Distinct().Count());
        foreach (var module in MigrationCatalog.Modules.Keys.Where(selection.Modules.Contains))
        {
            if (s.DatasetStates.GetValueOrDefault(module) == "NotApplicable")
            {
                if (string.IsNullOrWhiteSpace(s.ZeroEvidence.GetValueOrDefault(module))) result.Errors.Add(new(module, "", 0, "A signed zero/not-applicable declaration is missing."));
                if (allRows.TryGetValue(module, out var nonzero) && nonzero.Count > 0) result.Errors.Add(new(module, "", 0, "Uploaded detail rows conflict with the NotApplicable declaration."));
                continue;
            }
            if (!allRows.TryGetValue(module, out var data)) { result.Errors.Add(new(module, "", 0, "Awaiting data. Upload this module or select another available module.")); continue; }
            foreach (var error in MigrationJson.Read<List<string>>(datasets.Single(x => x.Module == module).ErrorsJson)) result.Errors.Add(new(module, "", 0, error));
            var kind = MigrationCatalog.ScopeKind(module);
            var selectedRows = data.Where(x => Selected(kind, x.Get("Code"))).ToList();
            foreach (var codes in kind switch { "Customer" => selection.Customers, "Supplier" => selection.Suppliers, "Stock" => selection.Skus, "Bank" => selection.Banks, _ => new List<string>() })
                if (!data.Any(x => x.Get("Code") == MigrationJson.Code(codes))) result.Errors.Add(new(module, codes, 0, "Selected code has no rows in this dataset. Supply evidence of a zero component in scope reconciliation; do not silently skip it."));
            foreach (var group in selectedRows.GroupBy(x => kind == "General" ? "ALL" : x.Get("Code")))
            {
                if (report != null) await report(totalUnits, result.Units.Count, module + "/" + group.Key, result.Units.Count(x => x.Errors.Count != 0));
                var u = new MigrationPreviewUnit { Module = module, Scope = group.Key, Rows = group.ToList() }; result.Units.Add(u);
                if (module.EndsWith("Master") && u.Rows.Count > 1) u.Errors.Add(new(module, group.Key, 0, "Master file must contain one row per unique legacy code."));
                if (MigrationCatalog.FinancialModules.Contains(module) && projectScopes.Any(x => x.Kind == kind && x.Code == group.Key && (x.Status == "Released" || x.Status == "NoLegacyOpening")) && !posted.Any(x => x.Module == module && x.ScopeKey == group.Key && x.Status == "Posted")) u.Errors.Add(new(module, group.Key, 0, "This scope has already been released/classified. Additional opening balances require a reviewed dated correction."));
                void Error(MigrationRow r, string message) => u.Errors.Add(new(module, group.Key, r.Row, message));
                int Required(MigrationRow r, string type, string field, string defaultKey = "")
                {
                    var value = Resolve(type, r.Get(field), defaultKey); if (value == 0) Error(r, $"Map {type} '{r.Get(field)}' ({field}) to a valid master."); r.Set("_" + field + "Id", value.ToString()); return value;
                }
                void AccountTypeCheck(MigrationRow row, string field, AccountType type)
                {
                    if (int.TryParse(row.Get("_" + field + "Id"), out var accountId) && accountId != 0 && accounts.Single(x => x.Id == accountId).Type != type) Error(row, $"{field} must map to an account of type {type}.");
                }
                foreach (var r in u.Rows)
                {
                    try
                    {
                        if (r.Get("Code").Length == 0 || r.Get("Code").Length > 100) Error(r, "A nonblank legacy code of up to 100 characters is required.");
                        if (MigrationCatalog.Identity(module, r).Length > 300) Error(r, "LegacyRecordId/composite key exceeds 300 characters. Supply a shorter stable ID.");
                        if (r.Get("LegacyRecordId") == "") u.Warnings.Add($"Row {r.Row}: using a composite legacy identity; verify that it is stable and unique.");
                        var existing = originsByIdentity.GetValueOrDefault((module, MigrationJson.Hash(MigrationCatalog.Identity(module, r))));
                        // Compare after business normalization below.
                        if (module is "CustomerMaster" or "SupplierMaster")
                        {
                            if (r.Get("Code").Length > 50 || r.Get("Name").Length is 0 or > 200) Error(r, "Master requires an actual name (max 200) and legacy code (max 50).");
                            foreach (var field in new[] { "Email", "Contact", "Phone", "Mobile", "Address1", "Address2", "TaxNumber" })
                            { var max = field is "Address1" or "Address2" ? 200 : field == "Email" ? 150 : field == "Contact" ? 100 : 50; if (r.Get(field).Length > max) Error(r, $"{field} exceeds {max} characters."); }
                            if (r.Get("Email") != "" && !new global::System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(r.Get("Email"))) Error(r, "The supplied email address is invalid; correct it or leave a genuine blank.");
                            if (new[] { "Email", "Contact", "Phone", "Address1", "TaxNumber" }.Any(f => r.Get(f) == "")) u.Warnings.Add($"{r.Get("Code")}: Details incomplete; genuine blanks will be retained.");
                            Required(r, kind + "Group", "GroupCode", kind + "Group"); Required(r, "PaymentTerm", "PaymentTermCode", kind + "PaymentTerm");
                            if (kind == "Customer") { Required(r, "PriceLevel", "PriceLevelCode", "PriceLevel"); Required(r, "Account", "ReceivableAccountCode", "ReceivableAccount"); Required(r, "Account", "RevenueAccountCode", "RevenueAccount"); }
                            else { Required(r, "Account", "PayableAccountCode", "PayableAccount"); Required(r, "Account", "ExpenseAccountCode", "ExpenseAccount"); }
                            if (kind == "Customer") { AccountTypeCheck(r, "ReceivableAccountCode", AccountType.Asset); AccountTypeCheck(r, "RevenueAccountCode", AccountType.Revenue); }
                            else { AccountTypeCheck(r, "PayableAccountCode", AccountType.Liability); AccountTypeCheck(r, "ExpenseAccountCode", AccountType.Expense); }
                            var partyId = kind == "Customer" ? customers.SingleOrDefault(x => x.LegacyCustomerCode == r.Get("Code"))?.Id : suppliers.SingleOrDefault(x => x.LegacySupplierCode == r.Get("Code"))?.Id;
                            var mapped = Resolve(kind, r.Get("Code")); if (mapped != 0) partyId = mapped;
                            if (partyId != null) { r.Set("_ExistingId", partyId.Value.ToString()); u.Warnings.Add($"{r.Get("Code")}: linking the existing {kind.ToLowerInvariant()} master; existing contact details will be preserved."); }
                            var boundCode = kind == "Customer" ? customers.SingleOrDefault(x => x.Id == partyId)?.LegacyCustomerCode : suppliers.SingleOrDefault(x => x.Id == partyId)?.LegacySupplierCode;
                            if (!string.IsNullOrWhiteSpace(boundCode) && boundCode != r.Get("Code")) Error(r, "Mapped master already belongs to another legacy code.");
                            if (kind == "Supplier" && r.Get("TaxNumber") != "" && suppliers.Any(x => x.TaxRegNo == r.Get("TaxNumber") && x.Id != partyId)) Error(r, "Supplied tax number already belongs to another supplier.");
                            if (kind == "Supplier" && r.Get("TaxNumber") != "" && data.Count(x => x.Get("TaxNumber") == r.Get("TaxNumber")) > 1) Error(r, "Supplied tax number appears on multiple supplier rows. Resolve the duplicate before import.");
                        }
                        else if (module is "CustomerOpenings" or "SupplierOpenings" or "ReceivedCheques" or "IssuedCheques")
                        {
                            var party = kind == "Customer" ? customers.SingleOrDefault(x => x.LegacyCustomerCode == r.Get("Code"))?.Id : suppliers.SingleOrDefault(x => x.LegacySupplierCode == r.Get("Code"))?.Id;
                            var mapped = Resolve(kind, r.Get("Code")); if (mapped != 0) party = mapped;
                            var masterModule = kind + "Master";
                            var pendingMaster = allRows.GetValueOrDefault(masterModule)?.SingleOrDefault(x => x.Get("Code") == r.Get("Code"));
                            if (party == null && (pendingMaster == null || !selection.Modules.Contains(masterModule))) Error(r, $"{kind} code is not mapped. Include its valid master in this run or import it first.");
                            var storedCode = kind == "Customer" ? customers.SingleOrDefault(x => x.Id == party)?.LegacyCustomerCode : suppliers.SingleOrDefault(x => x.Id == party)?.LegacySupplierCode;
                            if (!string.IsNullOrWhiteSpace(storedCode) && storedCode != r.Get("Code")) Error(r, "Mapped master already belongs to another legacy code. An alias cannot bypass party opening controls.");
                            var account = kind == "Customer" ? customers.SingleOrDefault(x => x.Id == party)?.DefaultReceivableAccountId : suppliers.SingleOrDefault(x => x.Id == party)?.DefaultPayableAccountId;
                            account ??= Resolve("Account", pendingMaster?.Get(kind == "Customer" ? "ReceivableAccountCode" : "PayableAccountCode") ?? "", kind == "Customer" ? "ReceivableAccount" : "PayableAccount");
                            if (!lookup["Account"].Contains(account ?? 0)) Error(r, "The party control account is missing or inactive."); r.Set("_ControlId", (account ?? 0).ToString());
                            if (accounts.SingleOrDefault(x => x.Id == account)?.Type != (kind == "Customer" ? AccountType.Asset : AccountType.Liability)) Error(r, "Party control must be a customer asset or supplier liability account.");
                            var amount = r.Decimal("Amount"); if (decimal.Round(amount, 2) != amount || Math.Abs(amount) >= 10000000000000000m || amount == 0) Error(r, "Amount must be a nonzero LKR value with two decimals.");
                            if (r.Get("Reference").Length is 0 or > 100) Error(r, "A document/receipt reference of up to 100 characters is required.");
                            if (module.EndsWith("Openings"))
                            {
                                if (r.Date("DocumentDate") > p.ClosingDate) Error(r, "Document date is after legacy closing.");
                                if (r.Get("DueDate") == "")
                                {
                                    if (kind == "Customer") r.Set("DueDate", r.Date("DocumentDate").AddDays(30).ToString("yyyy-MM-dd"));
                                    else if (s.SupplierTermDays != null) r.Set("DueDate", r.Date("DocumentDate").AddDays(s.SupplierTermDays.Value).ToString("yyyy-MM-dd"));
                                    else if (amount > 0) Error(r, "Supplier due date or approved supplier terms are required.");
                                }
                                if (r.Get("DueDate") != "" && r.Date("DueDate") < r.Date("DocumentDate")) Error(r, "Due date precedes document date.");
                                if (r.Get("Kind") is not "Invoice" and not "Bill" and not "ReturnCredit" and not "Credit" and not "Advance") Error(r, "Kind must be Invoice/Bill, ReturnCredit, Credit or Advance.");
                                if ((r.Get("Kind") is "ReturnCredit" or "Credit" or "Advance") != (amount < 0)) Error(r, "Credit/advance amounts must be negative and debt amounts positive.");
                                Add(u, account ?? 0, kind == "Customer" ? amount : -amount);
                            }
                            else
                            {
                                if (amount <= 0) Error(r, "Cheque amount must be positive.");
                                foreach (var flag in new[] { "PartyReflected", "BankReflected" }) if (r.Get(flag) is not "true" and not "false") Error(r, $"Explicit {flag} true/false is required; do not infer ledger treatment.");
                                if (!r.Flag("PartyReflected")) Error(r, "This opening cheque must already be reflected in the closing party ledger. Obtain a corrected closing export or an approved legacy adjustment first.");
                                if (module == "ReceivedCheques")
                                {
                                    Required(r, "BankBranch", "BankBranchCode"); Required(r, "Account", "SafeAccountCode", "SafeAccount");
                                    AccountTypeCheck(r, "SafeAccountCode", AccountType.Asset);
                                    if (r.Get("ChequeNumber").Length is 0 or > 50) Error(r, "Cheque number is required (max 50); retain leading zeros.");
                                    if (r.Date("ReceivedDate") > p.ClosingDate) Error(r, "Receipt date is after closing."); r.Date("ChequeDate");
                                    if (r.Get("Status") == "InSafe") { if (r.Flag("BankReflected")) Error(r, "An in-safe cheque cannot already be reflected in bank book balance."); Add(u, int.Parse(r.Get("_SafeAccountCodeId")), amount); }
                                    else if (r.Get("Status") == "Deposited") { Required(r, "Account", "BankAccountCode"); AccountTypeCheck(r, "BankAccountCode", AccountType.Asset); if (!r.Flag("BankReflected")) Error(r, "Deposited opening cheques must already be reflected in bank book balance; pending clearing is reconciled separately."); }
                                    else if (r.Get("Status") == "Endorsed") { if (Resolve("Supplier", r.Get("SupplierCode")) == 0 && !suppliers.Any(x => x.LegacySupplierCode == MigrationJson.Code(r.Get("SupplierCode"))) && !(selection.Modules.Contains("SupplierMaster") && allRows.GetValueOrDefault("SupplierMaster")?.Any(x => x.Get("Code") == MigrationJson.Code(r.Get("SupplierCode"))) == true)) Error(r, "Map/include the supplier master for this endorsed cheque before import."); if (r.Flag("BankReflected")) Error(r, "Endorsed cheque cannot also form an opening bank movement."); }
                                    else Error(r, "Opening received status must be InSafe, Deposited or Endorsed.");
                                }
                                else { Required(r, "Account", "BankAccountCode"); AccountTypeCheck(r, "BankAccountCode", AccountType.Asset); r.Date("ChequeDate"); if (r.Get("ChequeNumber").Length is 0 or > 50 || r.Date("DocumentDate") > p.ClosingDate) Error(r, "Issued cheque requires its cheque number and issue date at/before closing."); if (!r.Flag("BankReflected")) Error(r, "Issued opening cheques must already reduce bank book balance; obtain a corrected bank export before import."); }
                            }
                            if (party.HasValue && existing == null)
                            {
                                var activity = kind == "Customer" ? await db.SalesInvoices.AnyAsync(x => x.CustomerId == party && x.IsPosted, ct) || await db.PaymentTransactions.AnyAsync(x => x.CustomerId == party, ct) : await db.SupplierBills.AnyAsync(x => x.SupplierId == party && x.IsPosted, ct) || await db.PaymentTransactions.AnyAsync(x => x.SupplierId == party, ct);
                                if (activity) Error(r, "This party already has operational financial activity. Reconcile it and use a dated correction; opening import cannot overwrite it.");
                            }
                        }
                        else if (module == "Stock")
                        {
                            if (r.Get("UnitCost") == "" && r.Get("CandidateUnitCost") != "")
                            {
                                Error(r, "Legacy costval and average-cost valuation conflict. Supply corrected canonical unit costs or approve AverageCost with discrepancy evidence.");
                                r.Set("UnitCost", r.Get("CandidateUnitCost"));
                                u.Warnings.Add($"Row {r.Row}: source total costval {r.Get("SourceValue")}; candidate average unit cost {r.Get("CandidateUnitCost")}. The displayed journal is provisional and posting remains blocked.");
                            }
                            var qty = r.Decimal("Quantity"); var cost = r.Decimal("UnitCost");
                            if (qty < 0 || cost < 0 || decimal.Round(qty, 4) != qty || decimal.Round(cost, 6) != cost || qty >= 100000000000000m || cost >= 10000000000000000m) Error(r, "Stock quantity/cost must be nonnegative and fit 4/6 decimal places.");
                            if (qty == 0 || cost == 0) u.Warnings.Add($"Row {r.Row}: zero quantity or zero valuation requires review.");
                            var factor = r.Get("ConversionFactor") == "" ? 1m : r.Decimal("ConversionFactor");
                            if (factor <= 0 || decimal.Round(qty * factor, 4) != qty * factor || decimal.Round(cost / factor, 6) != cost / factor) Error(r, "Unit conversion must be positive and exactly representable at quantity/cost precision.");
                            if (factor > 0 && (qty * factor >= 100000000000000m || cost / factor >= 10000000000000000m)) Error(r, "Converted base quantity/cost exceeds storage precision.");
                            r.Set("_BaseQuantity", (qty * factor).ToString("G29", CultureInfo.InvariantCulture)); r.Set("_BaseCost", (cost / factor).ToString("G29", CultureInfo.InvariantCulture));
                            Required(r, "Warehouse", "WarehouseCode", "StockWarehouse"); var baseUnit = Required(r, "Unit", "UnitCode", "StockUnit");
                            var v = variants.SingleOrDefault(x => x.SKU == r.Get("Code")); var mappedVariant = Resolve("Stock", r.Get("Code")); if (mappedVariant != 0) v = variants.Single(x => x.Id == mappedVariant);
                            int inventory;
                            if (v != null)
                            {
                                r.Set("_ExistingId", v.Id.ToString()); inventory = v.Product.Category.InventoryAccountId ?? 0;
                                if (v.Product.UnitOfMeasureId != baseUnit) Error(r, "Map UnitCode to the existing product's base unit and supply the source-to-base conversion factor.");
                                var reversedStock = posted.Where(x => x.Module == "Stock" && x.ScopeKey == group.Key && x.Status == "Reversed").ToList();
                                var reversedReferences = reversedStock.SelectMany(x => new[] { $"MIG-{p.Id}-{x.Id}", $"MIG-REV-{x.Id}" }).ToList();
                                if (existing == null && await db.StockTransactions.AnyAsync(x => x.ProductVariantId == v.Id && !reversedReferences.Contains(x.ReferenceDocNo), ct)) Error(r, "SKU already has stock activity; use a controlled dated adjustment.");
                                if (existing == null && await db.StockLayers.AnyAsync(x => x.ProductVariantId == v.Id && x.RemainingQty != 0, ct)) Error(r, "SKU already has live FIFO layers; reconcile its existing stock instead of adding a second opening.");
                            }
                            else { Required(r, "Brand", "BrandCode"); var category = Required(r, "Category", "CategoryCode"); inventory = await db.Categories.Where(x => x.Id == category).Select(x => x.InventoryAccountId ?? 0).SingleOrDefaultAsync(ct); if (r.Get("Name").Length is 0 or > 200) Error(r, "New SKU requires a name (max 200)."); }
                            var warehouseAccount = warehouses.SingleOrDefault(x => x.Id == int.Parse(r.Get("_WarehouseCodeId")))?.OverrideInventoryAccountId;
                            if (warehouseAccount.HasValue) inventory = warehouseAccount.Value;
                            var overrideAccount = Resolve("Account", r.Get("InventoryAccountCode")); if (overrideAccount != 0 && inventory != overrideAccount) Error(r, "Inventory account must equal the warehouse/category posting account.");
                            if (!lookup["Account"].Contains(inventory)) Error(r, "Map the category to an active inventory account."); r.Set("_InventoryId", inventory.ToString());
                            if (r.Get("ProductType") is not "RawMaterial" and not "FinishedGood") Error(r, "ProductType must be RawMaterial or FinishedGood in this release.");
                            if (v != null && v.Product.Type.ToString() != r.Get("ProductType")) Error(r, "Existing SKU product type differs from source.");
                            if (r.Get("LayerDate") == "") { if (string.IsNullOrWhiteSpace(s.SummarizedStockEvidence)) Error(r, "FIFO layer date is missing. Supply original layers or record approval for a summarized opening layer."); r.Set("LayerDate", p.ClosingDate.ToString("yyyy-MM-dd")); }
                            if (r.Date("LayerDate") > p.ClosingDate) Error(r, "FIFO layer date is after closing.");
                            if (r.Get("CandidateUnitCost") != "")
                            {
                                if (s.FinishedGoodsValuation != "AverageCost" || string.IsNullOrWhiteSpace(s.FinishedGoodsValuationEvidence)) Error(r, "Legacy costval and average-cost valuation conflict. Supply corrected canonical costs or approve AverageCost with evidence.");
                                u.Warnings.Add($"Row {r.Row}: source costval {r.Get("SourceValue")}; selected warehouse value {qty * cost:N2}. Valuation evidence is retained.");
                            }
                            Add(u, inventory, decimal.Round(qty * cost, 2, MidpointRounding.AwayFromZero));
                            if (qty * cost >= 10000000000000000m) Error(r, "Stock valuation exceeds the maximum ledger amount.");
                        }
                        else if (module is "BankOpening" or "BankRecon")
                        {
                            r.Set("BankAccountCode", r.Get("Code")); var bankId = Required(r, "Account", "BankAccountCode");
                            if (data.Where(x => Resolve("Account", x.Get("Code")) == bankId).Select(x => x.Get("Code")).Distinct().Count() > 1 || module == "BankOpening" && u.Rows.Count != 1) Error(r, "One complete bank book opening is required per mapped bank account. Multiple legacy bank aliases cannot split its opening position.");
                            var amount = r.Decimal("Amount"); if (decimal.Round(amount, 2) != amount) Error(r, "Bank amount must have two decimal places.");
                            if (module == "BankOpening") { r.Decimal("StatementBalance"); Add(u, bankId, amount); }
                            else
                            {
                                if (r.Get("Reference") == "" || r.Date("DocumentDate") > p.ClosingDate) Error(r, "Bank recon item needs its reference and original date.");
                                if (r.Get("Kind") is not "OutstandingDeposit" and not "UnclearedPayment" and not "OtherBookItem") Error(r, "Bank recon Kind must be OutstandingDeposit, UnclearedPayment or OtherBookItem. Cheques belong in their own datasets.");
                                if (r.Get("Kind") == "OutstandingDeposit" && amount <= 0 || r.Get("Kind") == "UnclearedPayment" && amount >= 0) Error(r, "Outstanding deposits are positive and uncleared payments negative in the statement-to-book bridge.");
                                if (r.Get("BankReflected") is not "true" and not "false") Error(r, "Explicit BankReflected is required."); if (!r.Flag("BankReflected")) Error(r, "Unbooked bank adjustments must be resolved in the legacy closing book balance before import.");
                                foreach (var chequeModule in new[] { "ReceivedCheques", "IssuedCheques" })
                                    if (allRows.GetValueOrDefault(chequeModule)?.Any(c => Resolve("Account", c.Get("BankAccountCode")) == bankId && c.Get("Reference") == r.Get("Reference")) == true) Error(r, "This reference already appears in a cheque dataset. Do not duplicate it as a bank recon item.");
                            }
                            AccountTypeCheck(r, "BankAccountCode", AccountType.Asset);
                        }
                        else if (module == "Accounts")
                        {
                            if (r.Get("Name") == "" || !Enum.TryParse<AccountType>(r.Get("AccountType"), out var type) || !Enum.IsDefined(type)) Error(r, "Supply account name and valid AccountType.");
                            var account = accounts.SingleOrDefault(x => x.Code == r.Get("Code")); if (account != null && account.Type.ToString() != r.Get("AccountType")) Error(r, "Existing account type conflicts with source.");
                            if (r.Get("ParentCode") != "" && !accounts.Any(x => x.Code == r.Get("ParentCode") && !x.IsTransactionAccount)) Error(r, "Create/map the account folder before importing its transaction accounts.");
                        }
                        else if (module == "ControlTotals")
                        {
                            if (r.Get("Kind") is not "Customer" and not "Supplier" and not "Stock" and not "Bank" || r.Get("Component") == "") Error(r, "Control schedule needs a scope Kind and Component (the reconciliation control key).");
                            if (!int.TryParse(r.Get("RowCount"), out var count) || count < 0) Error(r, "Control schedule RowCount must be a nonnegative integer.");
                            if (decimal.Round(r.Decimal("Amount"), 4) != r.Decimal("Amount")) Error(r, "Control amount supports at most four decimal places.");
                            var component = r.Get("Component").Split(':');
                            if (r.Get("Kind") == "Stock" && component.Length == 3 && component[0] == "Stock")
                            { var warehouse = Resolve("Warehouse", component[1]); if (warehouse == 0) Error(r, "Map the control schedule warehouse code."); else r.Set("Component", "Stock:" + warehouses.Single(x => x.Id == warehouse).Code + ":" + component[2]); }
                        }
                        else if (module == "TrialBalance")
                        {
                            r.Set("AccountCode", r.Get("Code")); var acc = Required(r, "Account", "AccountCode");
                            var debit = r.Decimal("Debit"); var credit = r.Decimal("Credit");
                            if (debit < 0 || credit < 0 || debit != decimal.Round(debit, 2) || credit != decimal.Round(credit, 2) || debit != 0 && credit != 0) Error(r, "Trial balance needs nonnegative two-decimal Debit/Credit with only one side nonzero.");
                            // Only the migration opening contribution is subtracted; post-go-live trading is excluded.
                            var ownedJournalIds = posted.Where(x => x.Module != "TrialBalance" && x.Status == "Posted" && x.JournalEntryId != null).Select(x => x.JournalEntryId!.Value).ToArray();
                            var contribution = await db.JournalLines.Where(x => x.AccountId == acc && ownedJournalIds.Contains(x.JournalEntryId)).SumAsync(x => (decimal?)(x.Debit - x.Credit), ct) ?? 0;
                            var residual = debit - credit - contribution;
                            var managed = posted.Where(x => x.Module is not "Accounts" and not "CustomerMaster" and not "SupplierMaster" and not "TrialBalance").SelectMany(x => MigrationJson.Read<List<MigrationRow>>(x.RowsJson)).SelectMany(x => new[] { x.Get("_ControlId"), x.Get("_InventoryId"), x.Get("_BankAccountCodeId"), x.Get("_SafeAccountCodeId") }).Contains(acc.ToString());
                            if ((managed || managedAccounts.Contains(acc)) && residual != 0 && acc != s.OpeningEquityAccountId) Error(r, $"Managed control account residual is {residual:N2}. Correct the owning module; do not plug it in GL.");
                            Add(u, acc, residual);
                        }
                        foreach (var field in new[] { "Amount", "Quantity", "UnitCost", "ConversionFactor", "Debit", "Credit", "StatementBalance", "SellingPrice", "CandidateUnitCost", "SourceValue" })
                            if (r.Get(field) != "") r.Set(field, r.Decimal(field).ToString("G29", CultureInfo.InvariantCulture));
                        if (existing != null && existing.ContentHash != MigrationJson.Hash(r.Canonical)) Error(r, "Previously imported legacy record has changed. It cannot be overwritten; use an audited reversal/correction.");
                    }
                    catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentOutOfRangeException or DivideByZeroException) { Error(r, "Invalid or missing amount/date/conversion. Dates must be yyyy-MM-dd and numbers invariant decimal values."); }
                }
                foreach (var duplicate in u.Rows.GroupBy(r => MigrationCatalog.Identity(module, r)).Where(g => g.Count() > 1)) u.Errors.Add(new(module, group.Key, duplicate.First().Row, "Duplicate legacy identity/composite key. Supply unique stable LegacyRecordId values."));
                if (MigrationCatalog.FinancialModules.Contains(module))
                {
                    if (!lookup["Account"].Contains(s.OpeningEquityAccountId) || accounts.SingleOrDefault(x => x.Id == s.OpeningEquityAccountId)?.Type != AccountType.Equity) u.Errors.Add(new(module, group.Key, 0, "Select an active opening equity account."));
                    if (module != "TrialBalance") Add(u, s.OpeningEquityAccountId, -u.Journal.Sum(x => x.Debit - x.Credit));
                    if (!posted.Any(x => x.Module == module && x.ScopeKey == group.Key && x.Status == "Posted") && !await db.FinancialPeriods.AnyAsync(x => x.StartDate <= p.OpeningDate && x.EndDate >= p.OpeningDate && !x.IsClosed, ct)) u.Errors.Add(new(module, group.Key, 0, "The opening financial period is missing or closed."));
                }
                if (module == "TrialBalance")
                {
                    if (u.Journal.Sum(x => x.Debit - x.Credit) != 0) u.Errors.Add(new(module, group.Key, 0, "Source trial balance/residual journal is not balanced."));
                    var scopes = await db.MigrationScopes.Where(x => x.ProjectId == id && x.Status != "Released" && x.Status != "NoLegacyOpening").CountAsync(ct);
                    if (scopes != 0 || MigrationCatalog.FinancialModules.Where(x => x != "TrialBalance").Any(m => s.DatasetStates.GetValueOrDefault(m) == "AwaitingData")) u.Errors.Add(new(module, group.Key, 0, "Complete, reconcile and release all required opening scopes before posting residual GL."));
                    var mappedIds = u.Rows.Select(x => x.Get("_AccountCodeId")).ToHashSet();
                    if (lookup["Account"].Any(a => !mappedIds.Contains(a.ToString()))) u.Errors.Add(new(module, group.Key, 0, "Full closing trial balance must include every active transaction account, including zero-balance accounts or explicit mapped equivalents."));
                }
                u.Journal = u.Journal.GroupBy(x => x.AccountId).Select(x => new MigrationLine(x.Key, Math.Max(0, x.Sum(l => l.Debit - l.Credit)), Math.Max(0, -x.Sum(l => l.Debit - l.Credit)))).Where(x => x.Debit != 0 || x.Credit != 0).OrderBy(x => x.AccountId).ToList();
                if (u.Journal.Any(x => x.Debit >= 10000000000000000m || x.Credit >= 10000000000000000m)) u.Errors.Add(new(module, group.Key, 0, "The proposed journal exceeds the maximum ledger amount."));
                var resolvedKeys = new[] { "_WarehouseCodeId", "_UnitCodeId", "_BaseQuantity", "_BaseCost", "_ControlId", "_InventoryId", "_SafeAccountCodeId", "_BankAccountCodeId", "_BankBranchCodeId", "_AccountCodeId", "_ReceivableAccountCodeId", "_RevenueAccountCodeId", "_PayableAccountCodeId", "_ExpenseAccountCodeId", "_PriceLevelCodeId", "_PaymentTermCodeId", "_GroupCodeId" };
                u.Hash = MigrationJson.Hash(MigrationJson.Write(new { module, Scope = group.Key, Date = p.OpeningDate, Rows = u.Rows.OrderBy(x => x.Canonical).Select(x => new { Source = x.Canonical, Resolved = x.Data.Where(f => resolvedKeys.Contains(f.Key)).OrderBy(f => f.Key).ToDictionary(f => f.Key, f => f.Value) }).ToList(), u.Journal }));
                var previous = posted.SingleOrDefault(x => x.Module == module && x.ScopeKey == group.Key && x.Status == "Posted");
                if (previous != null) { u.Status = previous.Status == "Posted" ? "Already imported" : "Reversed"; if (previous.Hash != u.Hash) u.Errors.Add(new(module, group.Key, 0, "This posted scope has changed. A full original scope snapshot and dated correction are required.")); }
                if (u.Rows.Any(r => originsByIdentity.ContainsKey((module, MigrationJson.Hash(MigrationCatalog.Identity(module, r))))) && previous == null) u.Errors.Add(new(module, group.Key, 0, "Some records were imported in a different project/scope. They cannot be posted again."));
                u.Warnings = u.Warnings.Distinct().ToList();
                if (report != null) await report(totalUnits, result.Units.Count, module + "/" + group.Key, result.Units.Count(x => x.Errors.Count != 0));
            }
        }
        result.Hash = MigrationJson.Hash(MigrationJson.Write(new { p.Id, p.Version, p.SourceSystem, p.OpeningDate, p.ClosingDate, p.SettingsJson, Selection = selection, Units = result.Units.Select(x => x.Hash).OrderBy(x => x) }));
        return result;
    }
    private static void Add(MigrationPreviewUnit u, int account, decimal movement) { if (movement != 0) u.Journal.Add(new(account, Math.Max(movement, 0), Math.Max(-movement, 0))); }
}
