using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Services;
using NexusFlow.AppCore.Constants;
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
        public string? CashAndBankWarning { get; set; }
        public decimal OverdueReceivables { get; set; }
        public decimal CommittedSpend { get; set; }
        public int BouncedCheques { get; set; }
        public FinancialPeriodStatusDto? FinancialPeriodStatus { get; set; }
        public List<string> MissingNumberSequences { get; set; } = new();

        public List<string> ChartLabels { get; set; } = new();
        public List<decimal> SalesData { get; set; } = new();
        public List<decimal> PurchaseData { get; set; } = new();
    }

    public class GetDashboardDataQuery : IRequest<Result<DashboardMetricsDto>> { }

    public class GetDashboardDataHandler : IRequestHandler<GetDashboardDataQuery, Result<DashboardMetricsDto>>
    {
        private readonly IErpDbContext _context;
        private readonly IFinancialPeriodService _periodService;

        public GetDashboardDataHandler(IErpDbContext context, IFinancialPeriodService periodService)
        {
            _context = context;
            _periodService = periodService;
        }

        public async Task<Result<DashboardMetricsDto>> Handle(GetDashboardDataQuery request, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var sixMonthsAgo = startOfMonth.AddMonths(-5);

            var metrics = new DashboardMetricsDto();
            metrics.FinancialPeriodStatus = await _periodService.GetStatusAsync(DateTime.Today, cancellationToken);
            var sequenceNames = await _context.NumberSequences.AsNoTracking().Select(x => x.Module).ToListAsync(cancellationToken);
            metrics.MissingNumberSequences = Constants.NumberSequenceKeys.Required
                .Where(x => !sequenceNames.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();

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

            // Use the same dated subledgers as aging/statements: migration openings,
            // returns, settlements and reversals are part of the current party position.
            var customerIds = await _context.Customers.AsNoTracking().Select(x => x.Id).ToListAsync(cancellationToken);
            var customers = await new CustomerBalanceService(_context).GetManyAsync(customerIds, today, cancellationToken);
            metrics.AccountsReceivable = customers.Values.Sum(x => x.NetBalance);

            // 3. PURCHASING & AP METRICS
            var supplierIds = await _context.Suppliers.AsNoTracking().Select(x => x.Id).ToListAsync(cancellationToken);
            var suppliers = await new SupplierBalanceService(_context).GetManyAsync(supplierIds, today, cancellationToken);
            metrics.AccountsPayable = suppliers.Values.Sum(x => x.NetBalance);

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

            await PopulateCashAndBankAsync(metrics, cancellationToken);

            // Aging treats amounts due today as current. Unapplied credits remain
            // separate until applied to a debt, just as they do in the AR Aging report.
            metrics.OverdueReceivables = customers.Values.SelectMany(x => x.Debits)
                .Where(x => x.Balance > 0 && x.DueDate.Date < today.Date).Sum(x => x.Balance);

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

        private async Task PopulateCashAndBankAsync(DashboardMetricsDto metrics, CancellationToken ct)
        {
            // 1110 is the standard installed Cash and Cash Equivalents group. A custom
            // chart can explicitly select another group; account names/prefixes are never inferred.
            var rootCode = await _context.SystemConfigs.AsNoTracking().Where(x => x.Key == ConfigurationKeys.DashboardCashBankAccountRoot)
                .Select(x => x.Value).SingleOrDefaultAsync(ct) ?? "1110";
            var safeCode = await _context.SystemConfigs.AsNoTracking().Where(x => x.Key == AccountMappingKeys.UndepositedFunds)
                .Select(x => x.Value).SingleOrDefaultAsync(ct);
            var accounts = await _context.Accounts.AsNoTracking().Where(x => x.Type == AccountType.Asset)
                .Select(x => new { x.Id, x.Code, x.ParentAccountId, x.IsTransactionAccount, x.IsActive, x.Balance }).ToListAsync(ct);
            var root = accounts.SingleOrDefault(x => string.Equals(x.Code, rootCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (root == null || !root.IsActive || root.IsTransactionAccount || string.IsNullOrWhiteSpace(safeCode)
                || !accounts.Any(x => x.IsActive && x.IsTransactionAccount && string.Equals(x.Code, safeCode.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                metrics.CashAndBankWarning = "Cash and bank total is unavailable. Ask the finance administrator to review the cash and bank account group and undeposited funds mapping.";
                return;
            }
            var children = accounts.Where(x => x.ParentAccountId.HasValue).ToLookup(x => x.ParentAccountId!.Value);
            var pending = new Queue<int>();
            var visited = new HashSet<int>();
            pending.Enqueue(root.Id);
            while (pending.TryDequeue(out var parent))
            {
                if (!visited.Add(parent)) continue;
                foreach (var account in children[parent])
                {
                    if (!account.IsActive) continue;
                    if (account.IsTransactionAccount && !string.Equals(account.Code, safeCode.Trim(), StringComparison.OrdinalIgnoreCase))
                        metrics.CashOnHand += account.Balance;
                    pending.Enqueue(account.Id);
                }
            }
        }
    }
}
