using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Queries
{
    public class GetBalanceSheetHandler : IRequestHandler<GetBalanceSheetQuery, Result<BalanceSheetReport>>
    {
        private readonly IErpDbContext _context;

        public GetBalanceSheetHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<BalanceSheetReport>> Handle(GetBalanceSheetQuery request, CancellationToken cancellationToken)
        {
            var queryDate = request.AsOfDate.Date.AddDays(1).AddTicks(-1);

            // 1. Fetch ALL Balances (Grouped by Account)
            var rawBalances = await _context.JournalLines
                .Include(jl => jl.Account)
                .Where(jl => jl.JournalEntry.Date <= queryDate)
                .GroupBy(jl => new { jl.Account.Code, jl.Account.Name, jl.Account.Type })
                .Select(g => new
                {
                    g.Key.Code,
                    g.Key.Name,
                    g.Key.Type,
                    NetAmount = g.Sum(x => x.Debit - x.Credit) // Positive = Debit, Negative = Credit
                })
                .Where(x => x.NetAmount != 0) // Hide zero balance accounts
                .ToListAsync(cancellationToken);

            var report = new BalanceSheetReport { AsOfDate = request.AsOfDate };

            decimal totalRevenue = 0;
            decimal totalExpense = 0;

            // 2. Classify Accounts
            foreach (var item in rawBalances)
            {
                var line = new TrialBalanceLine
                {
                    AccountCode = item.Code,
                    AccountName = item.Name,
                    AccountType = item.Type.ToString(),
                    // For Assets: Debit is positive. For Liab/Equity: Credit (negative) is usually shown as positive on reports
                    // But for calculation, we keep raw signs: Debit(+), Credit(-)
                    Debit = item.NetAmount > 0 ? item.NetAmount : 0,
                    Credit = item.NetAmount < 0 ? Math.Abs(item.NetAmount) : 0
                };

                switch (item.Type)
                {
                    case AccountType.Asset:
                        report.Assets.Accounts.Add(line);
                        report.Assets.Total += item.NetAmount; // Assets are Debit (+)
                        break;

                    case AccountType.Liability:
                        report.Liabilities.Accounts.Add(line);
                        report.Liabilities.Total += Math.Abs(item.NetAmount); // Liabilities are Credit (-)
                        break;

                    case AccountType.Equity:
                        report.Equity.Accounts.Add(line);
                        report.Equity.Total += Math.Abs(item.NetAmount); // Equity is Credit (-)
                        break;

                    case AccountType.Revenue:
                        totalRevenue += Math.Abs(item.NetAmount); // Revenue is Credit
                        break;

                    case AccountType.Expense:
                        totalExpense += item.NetAmount; // Expense is Debit
                        break;
                }
            }

            // 3. Calculate Retained Earnings (Net Profit)
            // Profit = Revenue (Credit side) - Expenses (Debit side)
            // Note: In our signed math, Revenue is negative, Expense is positive. 
            // Let's stick to absolute values for clarity here:
            decimal netProfit = totalRevenue - totalExpense;

            // 4. Add Profit to Equity Section
            if (netProfit != 0)
            {
                report.Equity.Accounts.Add(new TrialBalanceLine
                {
                    AccountCode = "9999",
                    AccountName = "Current Period Earnings", // Dynamic Row
                    AccountType = "Equity",
                    Credit = netProfit > 0 ? netProfit : 0,
                    Debit = netProfit < 0 ? Math.Abs(netProfit) : 0
                });
                report.Equity.Total += netProfit;
            }

            return Result<BalanceSheetReport>.Success(report);
        }
    }
}
