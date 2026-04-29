using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Reporting.Queries
{
    public class GeneralLedgerRowDto
    {
        public string Date { get; set; } = string.Empty;
        public string JournalNo { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; } // Running Balance
    }

    public class GetGeneralLedgerQuery : IRequest<Result<List<GeneralLedgerRowDto>>>
    {
        public int AccountId { get; set; } // Enforced to ensure running balance math is accurate
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Module { get; set; } // e.g., "Purchasing", "Sales", "Manual"
    }

    public class GetGeneralLedgerHandler : IRequestHandler<GetGeneralLedgerQuery, Result<List<GeneralLedgerRowDto>>>
    {
        private readonly IErpDbContext _context;
        public GetGeneralLedgerHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<GeneralLedgerRowDto>>> Handle(GetGeneralLedgerQuery request, CancellationToken cancellationToken)
        {
            if (request.AccountId <= 0) return Result<List<GeneralLedgerRowDto>>.Failure("An Account must be selected.");

            var account = await _context.Accounts.FindAsync(new object[] { request.AccountId }, cancellationToken);
            if (account == null) return Result<List<GeneralLedgerRowDto>>.Failure("Account not found.");

            var endOfDay = request.EndDate.Date.AddDays(1).AddTicks(-1);
            var startOfDay = request.StartDate.Date;

            // 1. CALCULATE OPENING BALANCE (All journal lines before StartDate)
            var openingLines = await _context.JournalLines
                .Include(l => l.JournalEntry)
                .Where(l => l.AccountId == request.AccountId && l.JournalEntry.Date < startOfDay)
                .ToListAsync(cancellationToken);

            decimal openingDebit = openingLines.Sum(l => l.Debit);
            decimal openingCredit = openingLines.Sum(l => l.Credit);

            // Standard Accounting Math: Asset/Expense normally have Debit balances. 
            // Liability/Equity/Revenue normally have Credit balances.
            // For simplicity in a flat ledger, we'll calculate Net Change = Debit - Credit.
            decimal runningBalance = openingDebit - openingCredit;

            var ledger = new List<GeneralLedgerRowDto>
            {
                new GeneralLedgerRowDto
                {
                    Date = startOfDay.ToString("yyyy-MM-dd"),
                    JournalNo = "-",
                    Module = "System",
                    Description = "Opening Balance Brought Forward",
                    ReferenceNo = "-",
                    Debit = openingDebit,
                    Credit = openingCredit,
                    Balance = runningBalance
                }
            };

            // 2. FETCH TRANSACTIONS IN DATE RANGE
            var query = _context.JournalLines
                .Include(l => l.JournalEntry)
                .Where(l => l.AccountId == request.AccountId && l.JournalEntry.Date >= startOfDay && l.JournalEntry.Date <= endOfDay)
                .AsQueryable();

            if (!string.IsNullOrEmpty(request.Module))
                query = query.Where(l => l.JournalEntry.Module == request.Module);

            var transactions = await query
                .OrderBy(l => l.JournalEntry.Date)
                .ThenBy(l => l.Id)
                .ToListAsync(cancellationToken);

            // 3. PROCESS RUNNING BALANCE
            foreach (var t in transactions)
            {
                runningBalance += t.Debit;
                runningBalance -= t.Credit;

                ledger.Add(new GeneralLedgerRowDto
                {
                    Date = t.JournalEntry.Date.ToString("yyyy-MM-dd"),
                    JournalNo = $"JE-{t.JournalEntryId}",
                    Module = t.JournalEntry.Module,
                    Description = !string.IsNullOrEmpty(t.Description) ? t.Description : t.JournalEntry.Description,
                    ReferenceNo = t.JournalEntry.ReferenceNo,
                    Debit = t.Debit,
                    Credit = t.Credit,
                    Balance = runningBalance
                });
            }

            return Result<List<GeneralLedgerRowDto>>.Success(ledger);
        }
    }
}
