using System.Data;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Features.Treasury.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Services;

public sealed class CustomerSettlementService(IErpDbContext db, CustomerBalanceService balances)
{
    public static (string Type, int Id) Target(PaymentAllocationRequest a)
    {
        if ((a.InvoiceId > 0 ? 1 : 0) + (a.OpeningItemId.HasValue ? 1 : 0) + (a.DebitMemoId.HasValue ? 1 : 0) != 1)
            throw new InvalidOperationException("Each allocation must identify exactly one invoice, opening item or debit memo.");
        return a.OpeningItemId.HasValue ? ("OpeningItem", a.OpeningItemId.Value) : a.DebitMemoId.HasValue ? ("DebitMemo", a.DebitMemoId.Value) : ("Invoice", a.InvoiceId);
    }
    public async Task ValidateAsync(int customerId, DateTime date, List<PaymentAllocationRequest> requests, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == customerId, ct)
            ?? throw new InvalidOperationException("Customer not found.");
        if (await db.CustomerOpeningItems.AnyAsync(x => x.CustomerId == customerId && x.MigrationBatch.Status == "Posted" && x.PostingDate > date.Date, ct)
            || await db.ChequeRegisters.AnyAsync(x => x.CustomerId == customerId && x.MigrationBatchId != null && x.MigrationBatch!.Status == "Posted" && x.MigrationBatch.CutoverDate > date.Date, ct))
            throw new InvalidOperationException("Customer transaction date cannot precede migration cutover.");
        var snapshot = await balances.GetAsync(customerId, DateTime.MaxValue.Date.AddDays(-1), ct);
        var dated = await balances.GetAsync(customerId, date.Date, ct);
        var seen = new HashSet<(string, int)>();
        foreach (var request in requests)
        {
            var key = Target(request);
            var debit = snapshot.Debits.SingleOrDefault(x => x.TargetType == key.Type && x.Id == key.Id);
            var datedDebit = dated.Debits.SingleOrDefault(x => x.TargetType == key.Type && x.Id == key.Id);
            if (datedDebit == null || request.Amount > datedDebit.Balance || !seen.Add(key) || debit == null || request.Amount <= 0 || Math.Round(request.Amount, 2) != request.Amount || request.Amount > debit.Balance
                || debit.PostingDate > date.Date || debit.ReceivableAccountId != customer.DefaultReceivableAccountId)
                throw new InvalidOperationException("Invalid allocation: verify customer, account, date, duplicate targets and available amount.");
            // Prevent retroactive allocations that would corrupt an already established settlement history.
            if (await db.PaymentAllocations.AnyAsync(a =>
                    ((key.Type == "Invoice" && a.SalesInvoiceId == key.Id) || (key.Type == "OpeningItem" && a.CustomerOpeningItemId == key.Id)
                        || (key.Type == "DebitMemo" && a.CustomerDebitMemoId == key.Id)) && a.PaymentTransaction.Date > date.Date, ct)
                || await db.CustomerCreditApplications.AnyAsync(a =>
                    ((key.Type == "Invoice" && a.SalesInvoiceId == key.Id) || (key.Type == "OpeningItem" && a.CustomerOpeningItemId == key.Id)
                        || (key.Type == "DebitMemo" && a.CustomerDebitMemoId == key.Id)) && a.Date > date.Date, ct))
                throw new InvalidOperationException("Allocation predates an existing settlement. Use the latest settlement date or later.");
        }
    }
    public static PaymentAllocation Allocation(PaymentAllocationRequest a) => new()
    {
        SalesInvoiceId = a.InvoiceId > 0 ? a.InvoiceId : null, CustomerOpeningItemId = a.OpeningItemId,
        CustomerDebitMemoId = a.DebitMemoId, AmountAllocated = a.Amount
    };
    public async Task RefreshAsync(int customerId, CancellationToken ct)
    {
        var snapshot = await balances.GetAsync(customerId, DateTime.MaxValue.Date.AddDays(-1), ct);
        var invoices = await db.SalesInvoices.Where(x => x.CustomerId == customerId).ToListAsync(ct);
        var opening = await db.CustomerOpeningItems.Where(x => x.CustomerId == customerId).ToListAsync(ct);
        var memos = await db.CustomerDebitMemos.Where(x => x.CustomerId == customerId).ToListAsync(ct);
        var pendingIds = await db.ChequeRegisters.Where(x => x.CustomerId == customerId && x.OriginalReceiptId.HasValue
            && x.Status != ChequeStatus.Cleared && x.Status != ChequeStatus.Bounced).Select(x => x.OriginalReceiptId!.Value).ToListAsync(ct);
        var pending = await db.PaymentAllocations.Where(x => pendingIds.Contains(x.PaymentTransactionId)).ToListAsync(ct);
        foreach (var debit in snapshot.Debits)
        {
            var status = debit.Balance <= 0 ? InvoicePaymentStatus.Paid : debit.Settled > 0 ? InvoicePaymentStatus.Partial : InvoicePaymentStatus.Unpaid;
            if (status == InvoicePaymentStatus.Paid && pending.Any(a => debit.TargetType == "Invoice" ? a.SalesInvoiceId == debit.Id
                : debit.TargetType == "OpeningItem" ? a.CustomerOpeningItemId == debit.Id : a.CustomerDebitMemoId == debit.Id))
                status = InvoicePaymentStatus.PendingClearance;
            if (debit.TargetType == "Invoice") { var invoice = invoices.Single(x => x.Id == debit.Id); invoice.AmountPaid = debit.Settled; invoice.PaymentStatus = status; }
            else if (debit.TargetType == "OpeningItem") opening.Single(x => x.Id == debit.Id).AmountSettled = debit.Settled;
            else { var memo = memos.Single(x => x.Id == debit.Id); memo.AmountPaid = debit.Settled; memo.PaymentStatus = status; }
        }
        foreach (var credit in snapshot.OpeningCredits) opening.Single(x => x.Id == credit.Id).AmountSettled = -opening.Single(x => x.Id == credit.Id).Amount - credit.Available;
    }
    public async Task<int> ApplyCreditAsync(int customerId, int creditId, DateTime date, List<PaymentAllocationRequest> requests, CancellationToken ct)
    {
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await ValidateAsync(customerId, date, requests, ct);
        var credit = await db.CustomerOpeningItems.Include(x => x.MigrationBatch).FirstOrDefaultAsync(x => x.Id == creditId && x.CustomerId == customerId, ct);
        var snapshot = await balances.GetAsync(customerId, DateTime.MaxValue.Date.AddDays(-1), ct);
        var available = snapshot.OpeningCredits.FirstOrDefault(x => x.Id == creditId)?.Available ?? 0;
        var datedAvailable = (await balances.GetAsync(customerId, date, ct)).OpeningCredits.FirstOrDefault(x => x.Id == creditId)?.Available ?? 0;
        if (credit == null || credit.Amount >= 0 || credit.MigrationBatch.Status != "Posted" || date.Date < credit.PostingDate || requests.Count == 0
            || requests.Sum(x => x.Amount) > available || requests.Sum(x => x.Amount) > datedAvailable || await db.CustomerCreditApplications.AnyAsync(x => x.CreditOpeningItemId == creditId && x.Date > date.Date, ct))
            throw new InvalidOperationException("Credit is unavailable, amount is excessive, or application date precedes existing activity.");
        var targetAccounts = snapshot.Debits.Where(x => requests.Any(a => Target(a) == (x.TargetType, x.Id))).Select(x => x.ReceivableAccountId);
        if (targetAccounts.Any(x => x != credit.ReceivableAccountId)) throw new InvalidOperationException("Credit and debt must use the same AR account.");
        if (!await db.FinancialPeriods.AnyAsync(x => x.StartDate <= date.Date && x.EndDate >= date.Date && !x.IsClosed, ct))
            throw new InvalidOperationException("Credit application requires an open financial period.");
        foreach (var a in requests) db.CustomerCreditApplications.Add(new CustomerCreditApplication
        {
            CreditOpeningItemId = creditId, Date = date.Date, Amount = a.Amount,
            SalesInvoiceId = a.InvoiceId > 0 ? a.InvoiceId : null, CustomerOpeningItemId = a.OpeningItemId, CustomerDebitMemoId = a.DebitMemoId
        });
        await db.SaveChangesAsync(ct); await RefreshAsync(customerId, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return creditId;
    }
    public async Task ReverseCreditAsync(int applicationId, DateTime date, string reason, CancellationToken ct)
    {
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var a = await db.CustomerCreditApplications.Include(x => x.CreditOpeningItem).FirstOrDefaultAsync(x => x.Id == applicationId, ct)
            ?? throw new InvalidOperationException("Credit application not found.");
        if (a.ReversedDate != null || date.Date < a.Date || string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Valid reversal date and reason are required.");
        if (!await db.FinancialPeriods.AnyAsync(x => x.StartDate <= date.Date && x.EndDate >= date.Date && !x.IsClosed, ct))
            throw new InvalidOperationException("Credit reversal requires an open financial period.");
        a.ReversedDate = date.Date; a.ReversalReason = reason;
        await db.SaveChangesAsync(ct); await RefreshAsync(a.CreditOpeningItem.CustomerId, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
