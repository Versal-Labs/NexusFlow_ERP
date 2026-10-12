using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Services;

public sealed record SupplierDebitBalance(int Id, string TargetType, string Reference, DateTime Date, DateTime DueDate, decimal Amount, decimal Settled)
{
    public decimal Balance => Amount - Settled;
}
public sealed class SupplierBalanceSnapshot
{
    public List<CustomerLedgerEvent> Events { get; set; } = [];
    public List<SupplierDebitBalance> Debts { get; set; } = [];
    public decimal NetBalance => Events.Sum(x => x.Amount);
}
public sealed class SupplierBalanceService(IErpDbContext db)
{
    public async Task<SupplierBalanceSnapshot> GetAsync(int supplierId, DateTime asOf, CancellationToken ct)
    {
        var end = asOf.Date.AddDays(1); var result = new SupplierBalanceSnapshot();
        var bills = await db.SupplierBills.Where(x => x.SupplierId == supplierId && x.IsPosted && x.BillDate < end).ToListAsync(ct);
        var payments = await db.PaymentTransactions.Where(x => x.SupplierId == supplierId && x.Type == PaymentType.SupplierPayment && x.Date < end).ToListAsync(ct);
        var paymentIds = payments.Where(x => !x.IsVoided || (x.VoidDate ?? x.VoidedAt ?? x.Date) >= end).Select(x => x.Id).ToArray();
        var allocations = await db.PaymentAllocations.Where(x => paymentIds.Contains(x.PaymentTransactionId)).ToListAsync(ct);
        var units = await db.MigrationUnits.Where(x => x.Status == "Posted" || x.Status == "Reversed").ToListAsync(ct);
        var unitIds = units.Select(x => x.Id).ToList();
        var openings = await db.SupplierOpeningItems.Where(x => x.SupplierId == supplierId && x.PostingDate < end && unitIds.Contains(x.MigrationUnitId)).ToListAsync(ct);
        var openingIds = openings.Select(x => x.Id).ToArray(); var applications = await db.SupplierCreditApplications.Where(x => openingIds.Contains(x.CreditOpeningItemId) && x.Date < end).ToListAsync(ct);
        foreach (var bill in bills) { result.Events.Add(new(bill.BillDate, "AP Bill", bill.BillNumber, bill.SupplierInvoiceNo, bill.GrandTotal)); result.Debts.Add(new(bill.Id, "Bill", bill.BillNumber, bill.BillDate, bill.DueDate, bill.GrandTotal, allocations.Where(x => x.SupplierBillId == bill.Id).Sum(x => x.AmountAllocated) + applications.Where(x => x.SupplierBillId == bill.Id).Sum(x => x.Amount))); }
        foreach (var opening in openings)
        {
            result.Events.Add(new(opening.PostingDate, opening.Kind, opening.ReferenceNo, $"Opening item; original date {opening.DocumentDate:yyyy-MM-dd}", opening.Amount));
            if (units.Single(x => x.Id == opening.MigrationUnitId).ReversedDate is { } reverse && reverse < end)
            {
                result.Events.Add(new(reverse, "Opening reversal", opening.ReferenceNo, "Migration opening reversed", -opening.Amount));
                continue;
            }
            if (opening.Amount > 0) result.Debts.Add(new(opening.Id, "OpeningItem", opening.ReferenceNo, opening.DocumentDate, opening.DueDate ?? opening.DocumentDate, opening.Amount, allocations.Where(x => x.SupplierOpeningItemId == opening.Id).Sum(x => x.AmountAllocated) + applications.Where(x => x.SupplierOpeningItemId == opening.Id).Sum(x => x.Amount)));
        }
        foreach (var payment in payments) { result.Events.Add(new(payment.Date, "Supplier Payment", payment.ReferenceNo, payment.Method.ToString(), -payment.Amount)); if (payment.IsVoided && (payment.VoidDate ?? payment.VoidedAt ?? payment.Date) < end) result.Events.Add(new(payment.VoidDate ?? payment.VoidedAt ?? payment.Date, "Payment reversal", payment.ReferenceNo, payment.VoidReason ?? "Reversed", payment.Amount)); }
        return result;
    }
    public async Task ApplyCreditAsync(int creditId, int? openingId, int? billId, decimal amount, DateTime date, CancellationToken ct)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount || (openingId.HasValue ? 1 : 0) + (billId.HasValue ? 1 : 0) != 1) throw new InvalidOperationException("Choose one supplier debt target and a positive two-decimal credit amount.");
        using var tx = await db.BeginTransactionAsync(global::System.Data.IsolationLevel.Serializable, ct);
        if (!await db.FinancialPeriods.AnyAsync(x => x.StartDate <= date && x.EndDate >= date && !x.IsClosed, ct)) throw new InvalidOperationException("Credit application requires an open financial period.");
        var credit = await db.SupplierOpeningItems.SingleAsync(x => x.Id == creditId, ct);
        if (!await db.MigrationUnits.AnyAsync(x => x.Id == credit.MigrationUnitId && x.Status == "Posted", ct)) throw new InvalidOperationException("This opening credit was reversed.");
        if (credit.Amount >= 0 || date.Date < credit.PostingDate) throw new InvalidOperationException("Opening credit is invalid or predates go-live.");
        var available = -credit.Amount - (await db.SupplierCreditApplications.Where(x => x.CreditOpeningItemId == creditId).SumAsync(x => (decimal?)x.Amount, ct) ?? 0);
        var snapshot = await GetAsync(credit.SupplierId, date, ct); var target = snapshot.Debts.SingleOrDefault(x => x.Id == (openingId ?? billId) && x.TargetType == (openingId.HasValue ? "OpeningItem" : "Bill"));
        if (target == null || amount > available || amount > target.Balance) throw new InvalidOperationException("Credit/target belongs to another supplier or amount exceeds the available balance.");
        db.SupplierCreditApplications.Add(new() { CreditOpeningItemId = creditId, SupplierOpeningItemId = openingId, SupplierBillId = billId, Amount = amount, Date = date.Date }); await db.SaveChangesAsync(ct);
        if (billId.HasValue) { var bill = await db.SupplierBills.SingleAsync(x => x.Id == billId, ct); bill.AmountPaid += amount; bill.PaymentStatus = bill.AmountPaid >= bill.GrandTotal ? InvoicePaymentStatus.Paid : InvoicePaymentStatus.Partial; await db.SaveChangesAsync(ct); }
        await tx.CommitAsync(ct);
    }
}
