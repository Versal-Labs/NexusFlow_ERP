using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Services;

public sealed record OpeningBankResolution(DateTime Date, bool Cancel, string Reason);
public sealed class OpeningBankItemService(IErpDbContext db, IJournalService journals)
{
    public async Task ResolveAsync(int id, OpeningBankResolution request, CancellationToken ct)
    {
            using var tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var item = await db.OpeningBankItems.SingleAsync(x => x.Id == id, ct);
            var unit = await db.MigrationUnits.SingleAsync(x => x.Id == item.MigrationUnitId, ct); var project = await db.MigrationProjects.SingleAsync(x => x.Id == unit.ProjectId, ct);
            if (unit.Status != "Posted" || item.ClearedDate != null || item.CancelledDate != null || request.Date.Date < project.OpeningDate || request.Date.Date < item.Date.Date) throw new InvalidOperationException("This item is reversed/already resolved or the date precedes go-live/original date.");
            if (!request.Cancel && item.ChequeDate.HasValue && request.Date.Date < item.ChequeDate.Value.Date) throw new InvalidOperationException("A post-dated cheque cannot be cleared before its cheque date.");
            if (!await db.FinancialPeriods.AnyAsync(x => x.StartDate <= request.Date && x.EndDate >= request.Date && !x.IsClosed, ct)) throw new InvalidOperationException("Resolution requires an open financial period.");
            if (project.Status != "Finalized" && !await db.MigrationScopes.AnyAsync(x => x.ProjectId == project.Id && x.Kind == "Bank" && x.EntityId == item.BankAccountId && (x.Status == "Released" || x.Status == "NoLegacyOpening"), ct)) throw new InvalidOperationException("Reconcile and release this bank opening position before resolving its pending items.");
            if (request.Cancel)
            {
                if (item.Kind != "IssuedCheque" || item.SupplierId == null || string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("Cancellation is supported for an issued cheque and requires a reason. Use the cheque dishonour workflow for received cheques.");
                var supplier = await db.Suppliers.SingleAsync(x => x.Id == item.SupplierId, ct); if (!supplier.DefaultPayableAccountId.HasValue) throw new InvalidOperationException("Supplier control mapping is missing.");
                var amount = -item.Amount;
                db.SupplierOpeningItems.Add(new SupplierOpeningItem { MigrationUnitId = unit.Id, SupplierId = supplier.Id, PayableAccountId = supplier.DefaultPayableAccountId.Value, ReferenceNo = $"ISSUED-CANCEL-{item.Id}", Kind = "IssuedChequeCancellation", Amount = amount, DocumentDate = request.Date.Date, PostingDate = request.Date.Date, DueDate = request.Date.Date });
                var posted = await journals.PostJournalAsync(new JournalEntryRequest { Date = request.Date.Date, Module = "Treasury", ReferenceNo = $"ISSUED-CANCEL-{item.Id}", Description = request.Reason, Lines = [new() { AccountId = item.BankAccountId, Debit = amount }, new() { AccountId = supplier.DefaultPayableAccountId.Value, Credit = amount }] });
                if (!posted.Succeeded) throw new InvalidOperationException(posted.Message); item.ResolutionJournalId = posted.Data; item.CancelledDate = request.Date.Date;
            }
            else
            {
                // The opening bank book already includes this movement. Clearing posts no second journal.
                item.ClearedDate = request.Date.Date;
                if (item.Kind == "ReceivedCheque")
                {
                    var cheque = await db.ChequeRegisters.SingleAsync(x => x.Id == item.ReceivedChequeId, ct);
                    if (cheque.Status != ChequeStatus.Deposited) throw new InvalidOperationException("Received cheque is no longer deposited."); cheque.Status = ChequeStatus.Cleared; cheque.ClearedDate = request.Date.Date;
                }
            }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
