using System.Data;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Features.Migration;

public sealed record MigrationControl(string Key, decimal Source, decimal Posted, int Rows)
{
    public decimal Difference => Source - Posted;
}
public sealed partial class MigrationCentreService
{
    public async Task<List<MigrationControl>> ReconcileScopeAsync(int id, string kind, string code, CancellationToken ct)
    {
        var project = await Project(id, false, ct); code = MigrationJson.Code(code);
        var scope = await db.MigrationScopes.SingleAsync(x => x.ProjectId == id && x.Kind == kind && x.Code == code, ct);
        var units = await db.MigrationUnits.AsNoTracking().Where(x => x.ProjectId == id && x.ScopeKey == code && x.Status == "Posted").ToListAsync(ct);
        var result = new List<MigrationControl>();
        foreach (var module in MigrationJson.Read<List<string>>(scope.RequiredModulesJson))
        {
            var unit = units.SingleOrDefault(x => x.Module == module); var rows = unit == null ? [] : MigrationJson.Read<List<MigrationRow>>(unit.RowsJson);
            var originIds = unit == null ? [] : await db.MigrationOrigins.Where(x => x.UnitId == unit.Id).Select(x => x.EntityId).ToListAsync(ct);
            if (module == "CustomerOpenings")
            {
                var posted = await db.CustomerOpeningItems.Where(x => originIds.Contains(x.Id)).Select(x => x.Amount).ToListAsync(ct);
                result.Add(new(module + ":Debit", rows.Sum(x => Math.Max(0, x.Decimal("Amount"))), posted.Sum(x => Math.Max(0, x)), rows.Count));
                result.Add(new(module + ":Credit", rows.Sum(x => Math.Max(0, -x.Decimal("Amount"))), posted.Sum(x => Math.Max(0, -x)), rows.Count));
            }
            else if (module == "SupplierOpenings")
            {
                var posted = await db.SupplierOpeningItems.Where(x => originIds.Contains(x.Id)).Select(x => x.Amount).ToListAsync(ct);
                result.Add(new(module + ":Debit", rows.Sum(x => Math.Max(0, x.Decimal("Amount"))), posted.Sum(x => Math.Max(0, x)), rows.Count));
                result.Add(new(module + ":Credit", rows.Sum(x => Math.Max(0, -x.Decimal("Amount"))), posted.Sum(x => Math.Max(0, -x)), rows.Count));
            }
            else if (module == "ReceivedCheques") result.Add(new(module, rows.Sum(x => x.Decimal("Amount")), await db.ChequeRegisters.Where(x => originIds.Contains(x.Id)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0, rows.Count));
            else if (module == "IssuedCheques") result.Add(new(module, rows.Sum(x => x.Decimal("Amount")), -(await db.OpeningBankItems.Where(x => originIds.Contains(x.Id)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0), rows.Count));
            else if (module == "Stock")
            {
                foreach (var warehouse in rows.GroupBy(x => int.Parse(x.Get("_WarehouseCodeId"))))
                {
                    var layerIds = await db.MigrationOrigins.Where(x => x.UnitId == unit!.Id && x.EntityKind == "StockLayer").Select(x => x.EntityId).ToListAsync(ct);
                    var layers = await db.StockLayers.Where(x => layerIds.Contains(x.Id) && x.WarehouseId == warehouse.Key).ToListAsync(ct);
                    var warehouseCode = await db.Warehouses.Where(x => x.Id == warehouse.Key).Select(x => x.Code).SingleAsync(ct);
                    result.Add(new($"Stock:{warehouseCode}:Quantity", warehouse.Sum(x => x.Decimal("_BaseQuantity")), layers.Sum(x => x.InitialQty), warehouse.Count()));
                    result.Add(new($"Stock:{warehouseCode}:Value", warehouse.Sum(x => decimal.Round(x.Decimal("_BaseQuantity") * x.Decimal("_BaseCost"), 2, MidpointRounding.AwayFromZero)), layers.Sum(x => decimal.Round(x.InitialQty * x.UnitCost, 2, MidpointRounding.AwayFromZero)), warehouse.Count()));
                }
            }
            else if (module == "BankOpening")
            {
                var journalIds = units.Where(x => x.JournalEntryId != null).Select(x => x.JournalEntryId!.Value).ToArray();
                var bank = scope.EntityId ?? 0; var amount = rows.Sum(x => x.Decimal("Amount"));
                result.Add(new(module, amount, await db.JournalLines.Where(x => journalIds.Contains(x.JournalEntryId) && x.AccountId == bank).SumAsync(x => (decimal?)(x.Debit - x.Credit), ct) ?? 0, rows.Count));
                var bankUnitIds = await db.MigrationUnits.Where(x => x.ProjectId == id && x.Status == "Posted").Select(x => x.Id).ToListAsync(ct);
                var allBankItems = await db.OpeningBankItems.Where(x => x.BankAccountId == bank && bankUnitIds.Contains(x.MigrationUnitId)).ToListAsync(ct);
                // Reconcile the legacy closing snapshot, even after released instruments clear in new trading.
                var pending = allBankItems.Where(x => x.Date.Date <= project.ClosingDate && (x.ClearedDate == null || x.ClearedDate > project.ClosingDate) && (x.CancelledDate == null || x.CancelledDate > project.ClosingDate)).Sum(x => x.Amount);
                result.Add(new("BankStatementBridge", amount, rows.Sum(x => x.Decimal("StatementBalance")) + pending, allBankItems.Count));
            }
            else if (module == "BankRecon") result.Add(new(module, rows.Sum(x => x.Decimal("Amount")), await db.OpeningBankItems.Where(x => originIds.Contains(x.Id)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0, rows.Count));
        }
        return result;
    }
    public async Task ReleaseAsync(int id, string kind, string code, MigrationReleaseRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Evidence)) throw new InvalidOperationException("Record independent legacy control evidence and confirmation that the selected party/SKU/bank position is complete.");
        using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct); await db.LockMigrationProjectAsync(id, ct); var p = await Project(id, true, ct); await Editable(p, ct); code = MigrationJson.Code(code);
        var scope = await db.MigrationScopes.SingleAsync(x => x.ProjectId == id && x.Kind == kind && x.Code == code, ct);
        if (scope.Status is "Released" or "NoLegacyOpening") throw new InvalidOperationException("Scope is already released.");
        var settings = MigrationJson.Read<MigrationSettings>(p.SettingsJson);
        var requiredModules = MigrationJson.Read<List<string>>(scope.RequiredModulesJson);
        var datasets = await db.MigrationDatasets.AsNoTracking().Where(x => x.ProjectId == id && x.IsCurrent && requiredModules.Contains(x.Module)).ToListAsync(ct);
        foreach (var module in requiredModules)
        {
            var state = settings.DatasetStates.GetValueOrDefault(module);
            if (state == "AwaitingData") throw new InvalidOperationException($"{module} is still awaiting data.");
            if (state == "NotApplicable") { if (string.IsNullOrWhiteSpace(settings.ZeroEvidence.GetValueOrDefault(module))) throw new InvalidOperationException("Zero evidence is missing."); continue; }
            var d = datasets.SingleOrDefault(x => x.Module == module) ?? throw new InvalidOperationException("Required dataset is missing.");
            var sourceRows = StagedRows(d, settings).Where(x => x.Get("Code") == code).ToList();
            var isPosted = await db.MigrationUnits.AnyAsync(x => x.ProjectId == id && x.Module == module && x.ScopeKey == code && x.Status == "Posted", ct);
            if (sourceRows.Count != 0 && !isPosted) throw new InvalidOperationException($"{module} has unposted rows for this scope.");
            if (isPosted)
            {
                var selection = new MigrationSelection { Modules = [module] }; switch (kind) { case "Customer": selection.Customers.Add(code); break; case "Supplier": selection.Suppliers.Add(code); break; case "Stock": selection.Skus.Add(code); break; case "Bank": selection.Banks.Add(code); break; }
                var preview = await ValidateAsync(id, selection, ct); if (preview.Errors.Count != 0 || preview.Units.Any(x => x.Errors.Count != 0 || x.Status != "Already imported")) throw new InvalidOperationException("Current source differs from the posted scope. Resolve the conflict before release.");
            }
        }
        scope.EntityId = await EntityId(kind, code, settings, ct); if (scope.EntityId == null) throw new InvalidOperationException("The scope master is missing or unmapped.");
        var controls = await ReconcileScopeAsync(id, kind, code, ct);
        foreach (var c in controls)
            if (c.Difference != 0 || !request.Controls.TryGetValue(c.Key, out var expected) || expected != c.Source || !request.RowCounts.TryGetValue(c.Key, out var count) || count != c.Rows) throw new InvalidOperationException($"{c.Key}: posted totals, independently entered control and row count must match before release.");
        scope.Status = "Released"; scope.ReleasedAt = DateTime.UtcNow; scope.Evidence = MigrationJson.Write(request);
        if (owningOperationId is { } operationId)
        {
            var itemId = await db.MigrationOperationItems.Where(x => x.OperationId == operationId && x.ScopeId == scope.Id).Select(x => x.Id).SingleAsync(ct);
            await CompleteOperationItemAsync(operationId, itemId, null, ct);
        }
        Changed(p, "Released", $"{kind}/{code}: {request.Evidence}"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<List<MigrationControl>> ReconcileLedgerAsync(int id, CancellationToken ct, Func<MigrationControl, Task>? report = null)
    {
        var p = await Project(id, false, ct); var d = await db.MigrationDatasets.SingleOrDefaultAsync(x => x.ProjectId == id && x.Module == "TrialBalance" && x.IsCurrent, ct) ?? throw new InvalidOperationException("Full legacy closing trial balance is required.");
        var settings = MigrationJson.Read<MigrationSettings>(p.SettingsJson);
        var normalized = await ValidateAsync(id, new() { Modules = ["TrialBalance"] }, ct);
        var source = normalized.Units.SelectMany(x => x.Rows).ToList();
        var journalIds = await db.MigrationUnits.Where(x => x.ProjectId == id && x.Status == "Posted" && x.JournalEntryId != null).Select(x => x.JournalEntryId!.Value).ToListAsync(ct);
        var accounts = await db.Accounts.Where(x => x.IsTransactionAccount && x.IsActive).ToListAsync(ct); var result = new List<MigrationControl>();
        foreach (var account in accounts)
        {
            var rows = source.Where(r => r.Get("_AccountCodeId") == account.Id.ToString()).ToList();
            if (rows.Count == 0) throw new InvalidOperationException($"Closing trial balance is missing account {account.Code}, including its zero balance if appropriate.");
            result.Add(new(account.Code, rows.Sum(r => r.Decimal("Debit") - r.Decimal("Credit")), await db.JournalLines.Where(x => journalIds.Contains(x.JournalEntryId) && x.AccountId == account.Id).SumAsync(x => (decimal?)(x.Debit - x.Credit), ct) ?? 0, rows.Count));
            if (report != null) await report(result[^1]);
        }
        return result;
    }
    public async Task FinalizeAsync(int id, string evidence, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evidence)) throw new InvalidOperationException("Final financial sign-off evidence is required.");
        using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct); await db.LockMigrationProjectAsync(id, ct); var p = await Project(id, true, ct); await Editable(p, ct);
        var settings = MigrationJson.Read<MigrationSettings>(p.SettingsJson);
        if (MigrationCatalog.FinancialModules.Any(m => settings.DatasetStates.GetValueOrDefault(m) == "AwaitingData") || settings.DatasetStates.GetValueOrDefault("TrialBalance") != "Provided") throw new InvalidOperationException("Every required financial dataset and the full trial balance must be supplied or explicitly evidenced zero.");
        if (await db.MigrationScopes.AnyAsync(x => x.ProjectId == id && x.Status != "Released" && x.Status != "NoLegacyOpening", ct)) throw new InvalidOperationException("Pending scopes must be completed and released.");
        var datasets = await db.MigrationDatasets.Where(x => x.ProjectId == id && x.IsCurrent).ToListAsync(ct);
        var complete = await ValidateAsync(id, new() { Modules = MigrationCatalog.Modules.Keys.Where(m => settings.DatasetStates.GetValueOrDefault(m) == "Provided").ToList() }, ct);
        if (complete.Errors.Count != 0 || complete.Units.Any(x => x.Errors.Count != 0 || x.Status != "Already imported")) throw new InvalidOperationException("Every current source row must be validated and imported. Pending rows, changed posted snapshots or conflicting mappings prevent final sign-off.");
        var reconciliation = await ReconcileLedgerAsync(id, ct); if (reconciliation.Any(x => x.Difference != 0)) throw new InvalidOperationException("Migration opening journals do not match the source closing trial balance account by account.");
        p.Status = "Finalized"; p.FinalizedAt = DateTime.UtcNow; p.FinalEvidence = evidence; Audit(id, "Finalized", evidence); await CompleteSimpleOperationAsync(ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task ReverseUnitAsync(int id, int unitId, MigrationReversalRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("Reversal reason is required.");
        using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct); await db.LockMigrationProjectAsync(id, ct); var p = await Project(id, true, ct); await Editable(p, ct);
        var unit = await db.MigrationUnits.SingleAsync(x => x.ProjectId == id && x.Id == unitId, ct);
        if (unit.Status != "Posted" || request.Date.Date < p.OpeningDate) throw new InvalidOperationException("Only an unused posted unit can be reversed, dated on/after go-live.");
        var kind = MigrationCatalog.ScopeKind(unit.Module); var scope = await db.MigrationScopes.SingleOrDefaultAsync(x => x.ProjectId == id && x.Kind == kind && x.Code == unit.ScopeKey, ct);
        if (scope?.Status is "Released" or "NoLegacyOpening") throw new InvalidOperationException("Released scopes require a dated correction, not migration reversal.");
        if (unit.Module is "Accounts" or "CustomerMaster" or "SupplierMaster" or "BankOpening" or "TrialBalance" or "LegacyCustomerBatch") throw new InvalidOperationException("This unit has shared dependencies. Retain it and use a reviewed dated correction.");
        var ids = await db.MigrationOrigins.Where(x => x.UnitId == unitId).Select(x => x.EntityId).ToListAsync(ct);
        if (unit.Module is "CustomerOpenings" or "ReceivedCheques" && unit.JournalEntryId == null && await db.MigrationUnits.AnyAsync(x => x.ProjectId == id && x.Module == "LegacyCustomerBatch", ct)) throw new InvalidOperationException("Adopted legacy positions share their original batch journal. Keep the adopted batch read-only and use a dated correction.");
        if (unit.Module == "Stock") { var layers = await db.StockLayers.Where(x => ids.Contains(x.Id)).ToListAsync(ct); if (layers.Any(x => x.RemainingQty != x.InitialQty) || await db.StockTransactions.AnyAsync(x => x.ProductVariantId == scope!.EntityId && x.Type != StockTransactionType.OpeningBalance, ct)) throw new InvalidOperationException("Stock has subsequent activity. Use a dated adjustment."); using var lease = db.EnterMigrationPosting(id); foreach (var layer in layers) { layer.RemainingQty = 0; layer.IsExhausted = true; db.StockTransactions.Add(new() { ProductVariantId = layer.ProductVariantId, WarehouseId = layer.WarehouseId, Date = request.Date, Type = StockTransactionType.Adjustment, Qty = -layer.InitialQty, UnitCost = layer.UnitCost, TotalValue = -decimal.Round(layer.InitialQty * layer.UnitCost, 2, MidpointRounding.AwayFromZero), ReferenceDocNo = $"MIG-REV-{unitId}", Notes = request.Reason }); } await db.SaveChangesAsync(ct); }
        if (unit.Module == "CustomerOpenings" && (await db.PaymentAllocations.AnyAsync(x => x.CustomerOpeningItemId != null && ids.Contains(x.CustomerOpeningItemId.Value), ct) || await db.CustomerCreditApplications.AnyAsync(x => ids.Contains(x.CreditOpeningItemId) || x.CustomerOpeningItemId != null && ids.Contains(x.CustomerOpeningItemId.Value), ct))) throw new InvalidOperationException("Customer opening items have settlements. Use a dated correction.");
        if (unit.Module == "SupplierOpenings" && (await db.PaymentAllocations.AnyAsync(x => x.SupplierOpeningItemId != null && ids.Contains(x.SupplierOpeningItemId.Value), ct) || await db.SupplierCreditApplications.AnyAsync(x => ids.Contains(x.CreditOpeningItemId) || x.SupplierOpeningItemId != null && ids.Contains(x.SupplierOpeningItemId.Value), ct))) throw new InvalidOperationException("Supplier opening items have settlements. Use a dated correction.");
        if (unit.Module == "ReceivedCheques" && await db.ChequeRegisters.AnyAsync(x => ids.Contains(x.Id) && x.Status != ChequeStatus.InSafe, ct)) throw new InvalidOperationException("Cheque has downstream activity; use a dated correction.");
        if (unit.Module is "IssuedCheques" or "BankRecon" && await db.OpeningBankItems.AnyAsync(x => ids.Contains(x.Id) && (x.ClearedDate != null || x.CancelledDate != null), ct)) throw new InvalidOperationException("Bank item has downstream activity.");
        using (var lease = db.EnterMigrationPosting(id))
        {
            if (unit.JournalEntryId != null) { var lines = await db.JournalLines.Where(x => x.JournalEntryId == unit.JournalEntryId).ToListAsync(ct); var reverse = await journals.PostJournalAsync(new JournalEntryRequest { Date = request.Date.Date, Module = "MigrationCentreReversal", ReferenceNo = $"MIG-REV-{unitId}", Description = request.Reason, Lines = lines.Select(x => new JournalLineRequest { AccountId = x.AccountId, Debit = x.Credit, Credit = x.Debit }).ToList() }); if (!reverse.Succeeded) throw new InvalidOperationException(reverse.Message); unit.ReversalJournalEntryId = reverse.Data; }
            if (unit.Module is "CustomerOpenings" or "ReceivedCheques") { var batchIds = unit.Module == "CustomerOpenings" ? await db.CustomerOpeningItems.Where(x => ids.Contains(x.Id)).Select(x => x.MigrationBatchId).Distinct().ToListAsync(ct) : await db.ChequeRegisters.Where(x => ids.Contains(x.Id)).Select(x => x.MigrationBatchId!.Value).Distinct().ToListAsync(ct); foreach (var batch in await db.CustomerMigrationBatches.Where(x => batchIds.Contains(x.Id)).ToListAsync(ct)) { batch.Status = "Reversed"; batch.ReversedDate = request.Date.Date; batch.ReversalReason = request.Reason; batch.ReversalJournalEntryId = unit.ReversalJournalEntryId; } }
            unit.Status = "Reversed"; unit.ReversedDate = request.Date.Date; Changed(p, "Unit reversed", $"Unit {unitId}: {request.Reason}"); await db.SaveChangesAsync(ct);
            foreach (var origin in await db.MigrationOrigins.Where(x => x.UnitId == unitId).ToListAsync(ct)) origin.IsCurrent = false;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
