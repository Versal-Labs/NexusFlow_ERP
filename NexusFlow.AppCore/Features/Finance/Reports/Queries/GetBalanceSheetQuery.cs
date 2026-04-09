using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Reports.Queries
{
    // ==========================================
    // 1. DTOs
    // ==========================================
    public class BalanceSheetDto
    {
        public DateTime AsOfDate { get; set; }

        public List<AccountBalanceDto> AssetAccounts { get; set; } = new();
        public List<AccountBalanceDto> LiabilityAccounts { get; set; } = new();
        public List<AccountBalanceDto> EquityAccounts { get; set; } = new();

        public decimal TotalAssets => AssetAccounts.Sum(a => a.Balance);
        public decimal TotalLiabilities => LiabilityAccounts.Sum(a => a.Balance);
        public decimal TotalEquity => EquityAccounts.Sum(a => a.Balance);
        public decimal TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;

        // A quick check to ensure the math is flawless
        public bool IsBalanced => Math.Round(TotalAssets, 2) == Math.Round(TotalLiabilitiesAndEquity, 2);
    }

    // ==========================================
    // 2. THE QUERY
    // ==========================================
    public class GetBalanceSheetQuery : IRequest<Result<BalanceSheetDto>>
    {
        public DateTime AsOfDate { get; set; }
    }

    // ==========================================
    // 3. THE HANDLER
    // ==========================================
    public class GetBalanceSheetHandler : IRequestHandler<GetBalanceSheetQuery, Result<BalanceSheetDto>>
    {
        private readonly IErpDbContext _context;
        public GetBalanceSheetHandler(IErpDbContext context) => _context = context;

        public async Task<Result<BalanceSheetDto>> Handle(GetBalanceSheetQuery request, CancellationToken cancellationToken)
        {
            var report = new BalanceSheetDto { AsOfDate = request.AsOfDate };

            // 1. Fetch ALL POSTED GL Lines from the beginning of time up to the AsOfDate
            var lines = await _context.JournalLines
                .Include(jl => jl.Account)
                .Include(jl => jl.JournalEntry)
                .Where(jl => jl.JournalEntry.Date.Date <= request.AsOfDate.Date)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var groupedAccounts = lines.GroupBy(jl => new { jl.AccountId, jl.Account.Code, jl.Account.Name, jl.Account.Type }).ToList();

            decimal totalRevenue = 0;
            decimal totalExpense = 0;

            // 2. Process Every Account
            foreach (var group in groupedAccounts)
            {
                decimal totalDebits = group.Sum(x => x.Debit);
                decimal totalCredits = group.Sum(x => x.Credit);

                if (group.Key.Type == AccountType.Asset)
                {
                    // Assets are Debit Normal (Debits - Credits)
                    decimal balance = totalDebits - totalCredits;
                    if (balance != 0) report.AssetAccounts.Add(new AccountBalanceDto { AccountCode = group.Key.Code, AccountName = group.Key.Name, Balance = balance });
                }
                else if (group.Key.Type == AccountType.Liability)
                {
                    // Liabilities are Credit Normal (Credits - Debits)
                    decimal balance = totalCredits - totalDebits;
                    if (balance != 0) report.LiabilityAccounts.Add(new AccountBalanceDto { AccountCode = group.Key.Code, AccountName = group.Key.Name, Balance = balance });
                }
                else if (group.Key.Type == AccountType.Equity)
                {
                    // Equity is Credit Normal (Credits - Debits)
                    decimal balance = totalCredits - totalDebits;
                    if (balance != 0) report.EquityAccounts.Add(new AccountBalanceDto { AccountCode = group.Key.Code, AccountName = group.Key.Name, Balance = balance });
                }
                else if (group.Key.Type == AccountType.Revenue)
                {
                    totalRevenue += (totalCredits - totalDebits);
                }
                else if (group.Key.Type == AccountType.Expense)
                {
                    totalExpense += (totalDebits - totalCredits);
                }
            }

            // 3. TIER-1 ERP FEATURE: DYNAMIC RETAINED EARNINGS
            // The difference between all-time Revenue and all-time Expense belongs in Equity!
            decimal retainedEarnings = totalRevenue - totalExpense;

            if (retainedEarnings != 0)
            {
                report.EquityAccounts.Add(new AccountBalanceDto
                {
                    AccountCode = "3999", // Virtual code for display
                    AccountName = "Retained Earnings / Current Year Earnings",
                    Balance = retainedEarnings
                });
            }

            // 4. Sort for the Accountant
            report.AssetAccounts = report.AssetAccounts.OrderBy(a => a.AccountCode).ToList();
            report.LiabilityAccounts = report.LiabilityAccounts.OrderBy(a => a.AccountCode).ToList();
            report.EquityAccounts = report.EquityAccounts.OrderBy(a => a.AccountCode).ToList();

            return Result<BalanceSheetDto>.Success(report);
        }
    }
}
