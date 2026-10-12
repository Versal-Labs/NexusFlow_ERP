using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Entities.Sales;

namespace NexusFlow.AppCore.Features.Migration;

/// <summary>Commit-time guard covers commands, direct services, APIs and background/bulk writes.</summary>
public static class MigrationTradingGuard
{
    public static async Task CheckAsync(IErpDbContext db, IReadOnlyCollection<object> changes, CancellationToken ct)
    {
        var financial = changes.Where(x => x is PaymentTransaction or PaymentAllocation or ChequeRegister or CustomerDebitMemo or CustomerCreditApplication or SupplierCreditApplication or StockTransaction or StockLayer or CustomerOpeningItem or SupplierOpeningItem
            || x is SalesInvoice i && i.IsPosted || x is SupplierBill b && b.IsPosted || x is CreditNote n && n.IsPosted).ToList();
        var journals = changes.OfType<JournalEntry>().ToList();
        if (financial.Count == 0 && journals.Count == 0) return;
        var project = await db.MigrationProjects.AsNoTracking().SingleOrDefaultAsync(ct); if (project == null) return;
        var released = await db.MigrationScopes.AsNoTracking().Where(x => x.ProjectId == project.Id && (x.Status == "Released" || x.Status == "NoLegacyOpening") && x.EntityId != null).ToListAsync(ct);
        void Check(string kind, int id)
        {
            if (project.Status != "Finalized" && id > 0 && !released.Any(x => x.Kind == kind && x.EntityId == id)) throw new InvalidOperationException($"Migration Centre: {kind} {id} is locked until its opening position is reconciled and released (or classified No legacy opening).");
        }
        foreach (var entity in financial)
        {
            var dateField = entity is SalesInvoice ? "InvoiceDate" : entity is SupplierBill ? "BillDate" : "Date";
            if (entity.GetType().GetProperty(dateField)?.GetValue(entity) is DateTime postingDate && postingDate.Date < project.OpeningDate)
                throw new InvalidOperationException("Migration Centre: operational transactions cannot predate go-live. Keep earlier history in the read-only legacy archive.");
            if (entity is ChequeRegister openingCheque && openingCheque.OpeningEndorsedSupplierId is { } endorsedSupplier) Check("Supplier", endorsedSupplier);
            foreach (var pair in new[] { ("Customer", "CustomerId"), ("Supplier", "SupplierId"), ("Stock", "ProductVariantId") })
                if (entity.GetType().GetProperty(pair.Item2)?.GetValue(entity) is int id) Check(pair.Item1, id);
            if (entity is PaymentAllocation a)
            {
                if (a.PaymentTransaction?.CustomerId is { } cid) Check("Customer", cid); if (a.PaymentTransaction?.SupplierId is { } sid) Check("Supplier", sid);
            }
            if (entity is CustomerCreditApplication credit)
            {
                var id = await db.CustomerOpeningItems.Where(x => x.Id == credit.CreditOpeningItemId).Select(x => x.CustomerId).SingleAsync(ct); Check("Customer", id);
            }
            if (entity is SupplierCreditApplication supplierCredit)
            {
                var id = await db.SupplierOpeningItems.Where(x => x.Id == supplierCredit.CreditOpeningItemId).Select(x => x.SupplierId).SingleAsync(ct); Check("Supplier", id);
            }
        }
        foreach (var journal in journals)
            if (journal.Date.Date < project.OpeningDate) throw new InvalidOperationException("Migration Centre: operational journals cannot predate go-live.");
        // Final sign-off releases scope locks, while the legacy boundary remains permanent.
        if (project.Status == "Finalized") return;
        var bankScopes = await db.MigrationScopes.AsNoTracking().Where(x => x.ProjectId == project.Id && x.Kind == "Bank").ToListAsync(ct);
        var codes = bankScopes.Select(x => x.Code).ToList();
        var bankIds = await db.Accounts.Where(x => x.RequiresReconciliation || codes.Contains(x.Code)).Select(x => x.Id).ToListAsync(ct);
        bankIds.AddRange(bankScopes.Where(x => x.EntityId != null).Select(x => x.EntityId!.Value));
        var settings = MigrationJson.Read<MigrationSettings>(project.SettingsJson);
        if (settings.Mappings.TryGetValue("Account", out var accountMap)) bankIds.AddRange(bankScopes.Where(x => accountMap.ContainsKey(x.Code)).Select(x => accountMap[x.Code]));
        foreach (var journal in journals)
        {
            foreach (var line in journal.Lines) if (bankIds.Contains(line.AccountId)) Check("Bank", line.AccountId);
        }
    }
}
