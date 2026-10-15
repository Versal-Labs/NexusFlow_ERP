using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Services;

public sealed record CustomerLedgerEvent(DateTime Date, string Type, string Reference, string Description, decimal Amount);
public sealed record CustomerDebitBalance(int Id, string TargetType, string Reference, DateTime DocumentDate,
    DateTime DueDate, DateTime PostingDate, decimal Amount, decimal Settled, decimal Balance, int ReceivableAccountId);
public sealed record CustomerCreditBalance(int Id, string Reference, decimal Available);
public sealed class CustomerBalanceSnapshot
{
    public List<CustomerLedgerEvent> Events { get; set; } = [];
    public List<CustomerDebitBalance> Debits { get; set; } = [];
    public List<CustomerCreditBalance> OpeningCredits { get; set; } = [];
    public decimal NetBalance => Events.Sum(x => x.Amount);
    public decimal UnappliedCredit => Debits.Sum(x => x.Balance) - NetBalance;
}

/// <summary>Customer AR events and dated settlements share one definition across collections and reports.</summary>
public sealed class CustomerBalanceService(IErpDbContext db)
{
    public async Task<CustomerBalanceSnapshot> GetAsync(int customerId, DateTime asOf, CancellationToken ct = default) =>
        (await GetManyAsync([customerId], asOf, ct)).TryGetValue(customerId, out var value) ? value : throw new InvalidOperationException("Customer not found.");

    public async Task<Dictionary<int, CustomerBalanceSnapshot>> GetManyAsync(IEnumerable<int> ids, DateTime asOf, CancellationToken ct = default)
    {
        var end = asOf.Date.AddDays(1); var customerIds = ids.Distinct().ToArray();
        var customers = await db.Customers.Where(x => customerIds.Contains(x.Id)).ToListAsync(ct);
        var allInvoices = await db.SalesInvoices.Where(x => customerIds.Contains(x.CustomerId) && x.IsPosted && x.InvoiceDate < end).ToListAsync(ct);
        var allReceipts = await db.PaymentTransactions.Where(x => x.CustomerId.HasValue && customerIds.Contains(x.CustomerId.Value) && x.Type == PaymentType.CustomerReceipt && x.Date < end).ToListAsync(ct);
        var receiptIds = allReceipts.Select(x => x.Id).ToArray();
        var allAllocations = await db.PaymentAllocations.Where(x => receiptIds.Contains(x.PaymentTransactionId)).ToListAsync(ct);
        var allNotes = await db.CreditNotes.Where(x => customerIds.Contains(x.CustomerId) && x.IsPosted && x.Date < end).ToListAsync(ct);
        var allItems = await db.CustomerOpeningItems.AsNoTracking().Where(x => customerIds.Contains(x.CustomerId) && x.PostingDate < end && x.MigrationBatch.PostedAt != null)
            .Select(x => new { x.Id, x.CustomerId, x.PostingDate, x.Kind, x.ReferenceNo, x.DocumentDate, x.Amount, x.DueDate, x.ReceivableAccountId,
                x.MigrationBatch.ReversedDate, x.MigrationBatch.ReversalReason }).ToListAsync(ct);
        var allMemos = await db.CustomerDebitMemos.Where(x => customerIds.Contains(x.CustomerId) && x.Date < end).ToListAsync(ct);
        var allApplications = await db.CustomerCreditApplications.Include(x => x.CreditOpeningItem).Where(x => customerIds.Contains(x.CreditOpeningItem.CustomerId) && x.Date < end
            && (x.ReversedDate == null || x.ReversedDate >= end)).ToListAsync(ct);
        var snapshots = new Dictionary<int, CustomerBalanceSnapshot>();
        foreach (var customer in customers)
        {
            var customerId = customer.Id; var result = new CustomerBalanceSnapshot();
            var invoices = allInvoices.Where(x => x.CustomerId == customerId).ToList();
            var receipts = allReceipts.Where(x => x.CustomerId == customerId).ToList(); var idsForCustomer = receipts.Select(x => x.Id).ToHashSet();
            var allocations = allAllocations.Where(x => idsForCustomer.Contains(x.PaymentTransactionId)).ToList();
            var creditNotes = allNotes.Where(x => x.CustomerId == customerId).ToList();
            var items = allItems.Where(x => x.CustomerId == customerId).ToList(); var memos = allMemos.Where(x => x.CustomerId == customerId).ToList();
            var applications = allApplications.Where(x => x.CreditOpeningItem.CustomerId == customerId).ToList();
            DateTime? VoidDate(NexusFlow.Domain.Entities.Finance.PaymentTransaction r) => r.IsVoided ? r.VoidDate ?? r.VoidedAt?.Date ?? r.Date.Date : null;
            var activeReceipts = receipts.Where(r => !VoidDate(r).HasValue || VoidDate(r) >= end).Select(r => r.Id).ToHashSet();
            var activeAllocations = allocations.Where(a => activeReceipts.Contains(a.PaymentTransactionId)).ToList();
            foreach (var r in receipts)
            {
                result.Events.Add(new(r.Date.Date, "Payment Receipt", r.ReferenceNo, r.Method.ToString(), -r.Amount));
                if (VoidDate(r) is { } date && date < end)
                    result.Events.Add(new(date, "Receipt reversal", r.ReferenceNo, r.VoidReason ?? "Receipt reversed", r.Amount));
            }
            foreach (var i in invoices)
            {
                result.Events.Add(new(i.InvoiceDate.Date, "Invoice", i.InvoiceNumber, "Sales Invoice", i.GrandTotal));
                var settled = activeAllocations.Where(a => a.SalesInvoiceId == i.Id).Sum(a => a.AmountAllocated)
                    + applications.Where(a => a.SalesInvoiceId == i.Id).Sum(a => a.Amount);
                result.Debits.Add(new(i.Id, "Invoice", i.InvoiceNumber, i.InvoiceDate.Date, i.DueDate.Date, i.InvoiceDate.Date,
                    i.GrandTotal, settled, i.GrandTotal - settled, customer.DefaultReceivableAccountId));
            }
            foreach (var note in creditNotes) result.Events.Add(new(note.Date.Date, "Credit Note", note.CreditNoteNumber, note.Reason, -note.GrandTotal));
            foreach (var item in items)
            {
                result.Events.Add(new(item.PostingDate, item.Kind, item.ReferenceNo, $"Opening item; original date {item.DocumentDate:yyyy-MM-dd}", item.Amount));
                if (item.ReversedDate is { } reverse && reverse < end)
                {
                    result.Events.Add(new(reverse, "Opening reversal", item.ReferenceNo, item.ReversalReason ?? "Migration reversed", -item.Amount));
                    continue;
                }
                if (item.Amount > 0)
                {
                    var settled = activeAllocations.Where(a => a.CustomerOpeningItemId == item.Id).Sum(a => a.AmountAllocated)
                        + applications.Where(a => a.CustomerOpeningItemId == item.Id).Sum(a => a.Amount);
                    result.Debits.Add(new(item.Id, "OpeningItem", item.ReferenceNo, item.DocumentDate, item.DueDate ?? item.DocumentDate,
                        item.PostingDate, item.Amount, settled, item.Amount - settled, item.ReceivableAccountId));
                }
                else result.OpeningCredits.Add(new(item.Id, item.ReferenceNo, -item.Amount - applications.Where(a => a.CreditOpeningItemId == item.Id).Sum(a => a.Amount)));
            }
            foreach (var memo in memos)
            {
                result.Events.Add(new(memo.Date.Date, "Debit Memo", memo.DebitMemoNumber, memo.Reason, memo.Amount));
                var settled = activeAllocations.Where(a => a.CustomerDebitMemoId == memo.Id).Sum(a => a.AmountAllocated)
                    + applications.Where(a => a.CustomerDebitMemoId == memo.Id).Sum(a => a.Amount);
                result.Debits.Add(new(memo.Id, "DebitMemo", memo.DebitMemoNumber, memo.Date.Date, memo.DueDate ?? memo.Date.Date,
                    memo.Date.Date, memo.Amount, settled, memo.Amount - settled, memo.ReceivableAccountId ?? customer.DefaultReceivableAccountId));
            }
            snapshots[customerId] = result;
        }
        return snapshots;
    }
}
