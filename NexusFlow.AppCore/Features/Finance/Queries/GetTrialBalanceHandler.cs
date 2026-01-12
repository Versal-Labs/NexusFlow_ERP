using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Queries
{
    public class GetTrialBalanceHandler : IRequestHandler<GetTrialBalanceQuery, Result<TrialBalanceReport>>
    {
        private readonly IErpDbContext _context;

        public GetTrialBalanceHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<TrialBalanceReport>> Handle(GetTrialBalanceQuery request, CancellationToken cancellationToken)
        {
            // 1. Fetch Data
            // We group by Account and Sum Debits/Credits where Date <= Requested Date
            // We only care about "Posted" journals (Assuming all in DB are posted for now)

            var queryDate = request.AsOfDate.Date.AddDays(1).AddTicks(-1); // End of the day

            var balances = await _context.JournalLines
                .Include(jl => jl.Account)
                .Include(jl => jl.JournalEntry)
                .Where(jl => jl.JournalEntry.Date <= queryDate)
                .GroupBy(jl => new { jl.Account.Code, jl.Account.Name, jl.Account.Type })
                .Select(g => new
                {
                    AccountCode = g.Key.Code,
                    AccountName = g.Key.Name,
                    Type = g.Key.Type,
                    TotalDebit = g.Sum(x => x.Debit),
                    TotalCredit = g.Sum(x => x.Credit)
                })
                .OrderBy(x => x.AccountCode)
                .ToListAsync(cancellationToken);

            // 2. Construct Report
            var report = new TrialBalanceReport
            {
                AsOfDate = request.AsOfDate
            };

            foreach (var b in balances)
            {
                // Standard Accounting Display Logic:
                // - If Debit > Credit, show Net Debit.
                // - If Credit > Debit, show Net Credit.
                // - However, a "Trial Balance" typically shows the raw totals for both sides.

                report.Lines.Add(new TrialBalanceLine
                {
                    AccountCode = b.AccountCode,
                    AccountName = b.AccountName,
                    AccountType = b.Type.ToString(),
                    Debit = b.TotalDebit,
                    Credit = b.TotalCredit
                });
            }

            // 3. Calculate Totals
            report.TotalDebit = report.Lines.Sum(x => x.Debit);
            report.TotalCredit = report.Lines.Sum(x => x.Credit);

            return Result<TrialBalanceReport>.Success(report);
        }
    }
}
