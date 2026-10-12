using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Features.Finance.CustomerMigration;
using NexusFlow.Domain.Entities.Finance;

namespace NexusFlow.AppCore.Features.Migration;

public sealed partial class MigrationCentreService
{
    private async Task AdoptLegacyAsync(MigrationProject p, List<CustomerMigrationBatch> batches, string evidence, CancellationToken ct)
    {
        var settings = MigrationJson.Read<MigrationSettings>(p.SettingsJson); var source = new Dictionary<string, List<MigrationRow>> { ["CustomerOpenings"] = [], ["ReceivedCheques"] = [] };
        foreach (var batch in batches)
        {
            if (batch.JournalEntryId == null || !await db.JournalEntries.AnyAsync(x => x.Id == batch.JournalEntryId && x.Date == p.OpeningDate, ct)) throw new InvalidOperationException("Legacy batch journal is missing or has another posting date.");
            var review = MigrationJson.Read<CustomerMigrationReview>(batch.ReviewJson);
            if (settings.OpeningEquityAccountId != 0 && settings.OpeningEquityAccountId != review.OpeningBalanceAccountId) throw new InvalidOperationException("Legacy batches use different opening equity mappings. Reconcile them before adoption.");
            settings.OpeningEquityAccountId = review.OpeningBalanceAccountId; settings.Defaults["SafeAccount"] = review.UndepositedAccountId;
            settings.Mappings.TryAdd("BankBranch", new()); foreach (var bank in review.Banks) settings.Mappings["BankBranch"][MigrationJson.Code(bank.Code)] = bank.BankBranchId;
            db.MigrationUnits.Add(new() { ProjectId = p.Id, Module = "LegacyCustomerBatch", ScopeKey = batch.Id.ToString(), Hash = MigrationJson.Hash("ADOPT:" + batch.Id + ":" + batch.SourceHash), RowsJson = "[]", JournalEntryId = batch.JournalEntryId });
            Audit(p.Id, "Legacy batch adopted", $"Batch {batch.Id}; journal {batch.JournalEntryId}; original files remain in legacy batch audit. {evidence}");
        }
        var ids = batches.Select(x => x.Id).ToList(); var customers = await db.Customers.ToListAsync(ct);
        foreach (var item in await db.CustomerOpeningItems.Where(x => ids.Contains(x.MigrationBatchId)).ToListAsync(ct))
        {
            if (customers.Single(x => x.Id == item.CustomerId).DefaultReceivableAccountId != item.ReceivableAccountId) throw new InvalidOperationException("Customer control mapping changed after legacy posting; reconcile before adoption.");
            var row = AdoptionRow("CustomerOpenings", item.SourceRow, ("Code", item.LegacyCustomerCode), ("Reference", item.ReferenceNo), ("Kind", item.Amount < 0 ? "ReturnCredit" : "Invoice"), ("DocumentDate", item.DocumentDate.ToString("yyyy-MM-dd")), ("DueDate", (item.DueDate ?? item.DocumentDate.AddDays(30)).ToString("yyyy-MM-dd")), ("Amount", item.Amount.ToString("G29", global::System.Globalization.CultureInfo.InvariantCulture)));
            row.Set("_EntityId", item.Id.ToString()); row.Set("_ControlId", item.ReceivableAccountId.ToString()); source["CustomerOpenings"].Add(row);
        }
        foreach (var cheque in await db.ChequeRegisters.Where(x => x.MigrationBatchId != null && ids.Contains(x.MigrationBatchId.Value)).ToListAsync(ct))
        {
            if (cheque.Status != NexusFlow.Domain.Enums.ChequeStatus.InSafe) throw new InvalidOperationException("A legacy cheque has downstream activity. Keep its legacy batch read-only and reconcile an adoption before moving it into the new wizard.");
            var code = customers.Single(x => x.Id == cheque.CustomerId).LegacyCustomerCode ?? throw new InvalidOperationException("Legacy cheque customer code is missing.");
            var branchCode = settings.Mappings["BankBranch"].Single(x => x.Value == cheque.BankBranchId).Key;
            var row = AdoptionRow("ReceivedCheques", cheque.OpeningSourceRow ?? 0, ("Code", code), ("Reference", cheque.LegacyReceiptReference ?? ""), ("DocumentReferences", cheque.LegacyDocumentReferences ?? ""), ("BankBranchCode", branchCode), ("ChequeNumber", cheque.ChequeNumber), ("ChequeDate", cheque.ChequeDate.ToString("yyyy-MM-dd")), ("ReceivedDate", (cheque.ReceivedDate ?? p.ClosingDate).ToString("yyyy-MM-dd")), ("Amount", cheque.Amount.ToString("G29", global::System.Globalization.CultureInfo.InvariantCulture)), ("Status", "InSafe"), ("PartyReflected", "true"), ("BankReflected", "false"));
            row.Set("_EntityId", cheque.Id.ToString()); row.Set("_ControlId", cheque.OpeningReceivableAccountId!.Value.ToString()); source["ReceivedCheques"].Add(row);
        }
        p.SettingsJson = MigrationJson.Write(settings);
        foreach (var module in source)
        {
            using var memory = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes(MigrationJson.Write(module.Value)));
            var storageRef = await storage.SaveFileSecurelyAsync(memory, "adopted-" + module.Key + ".json", "migration/" + p.Id, "application/json", ct);
            db.MigrationDatasets.Add(new() { ProjectId = p.Id, Module = module.Key, Revision = 1, ClosingDate = p.ClosingDate, FileName = "adopted-" + module.Key + ".json", FileHash = MigrationJson.Hash(MigrationJson.Write(module.Value)), StorageReference = storageRef, RowsJson = MigrationJson.Write(module.Value) }); settings.DatasetStates[module.Key] = "Provided";
            foreach (var group in module.Value.GroupBy(x => x.Get("Code")))
            {
                var unit = new MigrationUnit { ProjectId = p.Id, Module = module.Key, ScopeKey = group.Key, RowsJson = MigrationJson.Write(group.ToList()) };
                db.MigrationUnits.Add(unit); await db.SaveChangesAsync(ct);
                foreach (var row in group)
                {
                    var identity = MigrationCatalog.Identity(module.Key, row);
                    db.MigrationOrigins.Add(new() { ProjectId = p.Id, UnitId = unit.Id, SourceSystem = p.SourceSystem, RecordKind = module.Key, IdentityHash = MigrationJson.Hash(identity), ContentHash = MigrationJson.Hash(row.Canonical), LegacyRecordId = identity, EntityKind = module.Key == "CustomerOpenings" ? "CustomerOpeningItem" : "Cheque", EntityId = int.Parse(row.Get("_EntityId")) });
                }
            }
        }
        p.SettingsJson = MigrationJson.Write(settings); await db.SaveChangesAsync(ct);
        foreach (var code in source.Values.SelectMany(x => x).Select(x => x.Get("Code")).Distinct()) db.MigrationScopes.Add(new() { ProjectId = p.Id, Kind = "Customer", Code = code, EntityId = customers.Single(x => x.LegacyCustomerCode == code).Id, RequiredModulesJson = MigrationJson.Write(DefaultRequirements("Customer")), Evidence = "Adopted legacy opening: " + evidence });
        await db.SaveChangesAsync(ct);
        var preview = await ValidateAsync(p.Id, new() { Modules = ["CustomerOpenings", "ReceivedCheques"] }, ct);
        foreach (var u in preview.Units)
        {
            if (u.Errors.Any(x => !x.Message.StartsWith("This posted scope has changed"))) throw new InvalidOperationException("Legacy adoption failed current validation: " + string.Join("; ", u.Errors.Select(x => x.Message)));
            var stored = await db.MigrationUnits.SingleAsync(x => x.ProjectId == p.Id && x.Module == u.Module && x.ScopeKey == u.Scope, ct); stored.Hash = u.Hash; stored.RowsJson = MigrationJson.Write(u.Rows);
        }
        await db.SaveChangesAsync(ct);
    }
    private static MigrationRow AdoptionRow(string module, int rowNo, params (string Key, string Value)[] fields)
    {
        var row = new MigrationRow { Row = rowNo }; foreach (var field in MigrationCatalog.Modules[module]) row.Set(field, ""); foreach (var (key, value) in fields) row.Set(key, value); return row;
    }
}
