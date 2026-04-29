using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Dashboard.Queries
{
    public class DashboardMetricsDto
    {
        public decimal TotalInventoryValue { get; set; }
        public int LowStockItems { get; set; }
        public int PendingSalesOrders { get; set; }
        public decimal MtdRevenue { get; set; }
        public decimal AccountsReceivable { get; set; }
        public decimal AccountsPayable { get; set; }
        public decimal CashOnHand { get; set; }
        public decimal OverdueReceivables { get; set; }
        public decimal CommittedSpend { get; set; }
        public int BouncedCheques { get; set; }

        public List<string> ChartLabels { get; set; } = new();
        public List<decimal> SalesData { get; set; } = new();
        public List<decimal> PurchaseData { get; set; } = new();
    }

    public class GetDashboardDataQuery : IRequest<Result<DashboardMetricsDto>> { }

    public class GetDashboardDataHandler : IRequestHandler<GetDashboardDataQuery, Result<DashboardMetricsDto>>
    {
        private readonly IErpDbContext _context;

        public GetDashboardDataHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<DashboardMetricsDto>> Handle(GetDashboardDataQuery request, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var sixMonthsAgo = startOfMonth.AddMonths(-5);

            var metrics = new DashboardMetricsDto();

            // 1. INVENTORY METRICS
            metrics.TotalInventoryValue = await _context.StockLayers
                .Where(s => s.RemainingQty > 0 && !s.IsExhausted)
                .SumAsync(s => s.RemainingQty * s.UnitCost, cancellationToken);

            // Group by variant to find actual stock levels for low stock alerts
            var stockGroups = await _context.StockLayers
                .Where(s => s.RemainingQty > 0)
                .GroupBy(s => s.ProductVariantId)
                .Select(g => new { VariantId = g.Key, TotalQty = g.Sum(x => x.RemainingQty) })
                .ToListAsync(cancellationToken);

            metrics.LowStockItems = stockGroups.Count(x => x.TotalQty < 50); // Hardcoded threshold for now

            // 2. SALES & AR METRICS
            metrics.PendingSalesOrders = await _context.SalesOrders
                .CountAsync(o => o.Status == SalesOrderStatus.Draft || o.Status == SalesOrderStatus.Converted, cancellationToken);

            metrics.MtdRevenue = await _context.SalesInvoices
                .Where(i => i.IsPosted && i.InvoiceDate >= startOfMonth)
                .SumAsync(i => i.GrandTotal, cancellationToken);

            metrics.AccountsReceivable = await _context.SalesInvoices
                .Where(i => i.IsPosted && i.PaymentStatus != InvoicePaymentStatus.Paid)
                .SumAsync(i => i.GrandTotal - i.AmountPaid, cancellationToken);

            // 3. PURCHASING & AP METRICS
            metrics.AccountsPayable = await _context.SupplierBills
                .Where(b => b.IsPosted && b.PaymentStatus != InvoicePaymentStatus.Paid)
                .SumAsync(b => b.GrandTotal - b.AmountPaid, cancellationToken);

            // 4. 6-MONTH CHART DATA (Sales vs Purchases)
            var salesHistory = await _context.SalesInvoices
                .Where(i => i.IsPosted && i.InvoiceDate >= sixMonthsAgo)
                .GroupBy(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(i => i.GrandTotal) })
                .ToListAsync(cancellationToken);

            var purchaseHistory = await _context.SupplierBills
                .Where(b => b.IsPosted && b.BillDate >= sixMonthsAgo)
                .GroupBy(b => new { b.BillDate.Year, b.BillDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(b => b.GrandTotal) })
                .ToListAsync(cancellationToken);

            metrics.CashOnHand = await _context.Accounts
        .Where(a => a.IsActive && (a.Type == NexusFlow.Domain.Enums.AccountType.Asset)) // Adjust AccountType enum as per your setup for Bank/Cash
        .SumAsync(a => a.Balance, cancellationToken);

            // 2. Overdue Receivables (Invoices past due date)
            metrics.OverdueReceivables = await _context.SalesInvoices
                .Where(i => i.IsPosted && i.PaymentStatus != InvoicePaymentStatus.Paid && i.DueDate < today)
                .SumAsync(i => i.GrandTotal - i.AmountPaid, cancellationToken);

            // 3. Committed Spend (Approved POs waiting for delivery/billing)
            metrics.CommittedSpend = await _context.PurchaseOrders
                .Where(p => p.Status == PurchaseOrderStatus.Approved || p.Status == PurchaseOrderStatus.Partial)
                .SumAsync(p => p.TotalAmount, cancellationToken);

            // 4. Treasury Alerts (Bounced Cheques)
            metrics.BouncedCheques = await _context.ChequeRegisters
                .CountAsync(c => c.Status == NexusFlow.Domain.Enums.ChequeStatus.Bounced, cancellationToken);

            // Build the chronological arrays for the chart
            for (int i = 5; i >= 0; i--)
            {
                var targetMonth = today.AddMonths(-i);
                metrics.ChartLabels.Add(targetMonth.ToString("MMM yyyy"));

                var monthSales = salesHistory.FirstOrDefault(s => s.Year == targetMonth.Year && s.Month == targetMonth.Month)?.Total ?? 0;
                var monthPurchases = purchaseHistory.FirstOrDefault(p => p.Year == targetMonth.Year && p.Month == targetMonth.Month)?.Total ?? 0;

                metrics.SalesData.Add(monthSales);
                metrics.PurchaseData.Add(monthPurchases);
            }

            return Result<DashboardMetricsDto>.Success(metrics);
        }
    }
}
