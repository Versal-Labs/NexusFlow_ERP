using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Services
{
    public class JournalService : IJournalService
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequenceService;

        // Inject the centralized Sequence Service to prevent DB locking/race conditions
        public JournalService(IErpDbContext context, INumberSequenceService sequenceService)
        {
            _context = context;
            _sequenceService = sequenceService;
        }

        public async Task<Result<int>> PostJournalAsync(JournalEntryRequest request)
        {
            // Use standard cancellation token pattern
            var ct = CancellationToken.None;

            // =============================================================
            // RULE 1: STRICT PERIOD LOCKING
            // =============================================================
            var period = await _context.FinancialPeriods
                .FirstOrDefaultAsync(p => request.Date.Date >= p.StartDate.Date && request.Date.Date <= p.EndDate.Date, ct);

            // FAIL if period does not exist OR is closed.
            if (period == null)
            {
                return Result<int>.Failure($"Accounting Error: No Financial Period exists for the date {request.Date:yyyy-MM-dd}. Please open the period first.");
            }
            if (period.IsClosed)
            {
                return Result<int>.Failure($"Accounting Error: The Financial Period '{period.Year}-{period.Month}' is CLOSED. You cannot post transactions to this date.");
            }

            // =============================================================
            // RULE 2: STRICT DOUBLE ENTRY (The Golden Rule)
            // =============================================================
            decimal totalDebits = request.Lines.Sum(x => x.Debit);
            decimal totalCredits = request.Lines.Sum(x => x.Credit);

            if (Math.Abs(totalDebits - totalCredits) > 0.00m)
            {
                return Result<int>.Failure($"Double Entry Violation: Debits ({totalDebits:N2}) do not equal Credits ({totalCredits:N2}). Difference: {totalDebits - totalCredits}");
            }

            if (totalDebits == 0)
                return Result<int>.Failure("Transaction cannot be zero.");

            // =============================================================
            // RULE 3: ACCOUNT VALIDATION & BALANCE UPDATES
            // =============================================================
            var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();

            // Note: We do NOT use AsNoTracking() here because we MUST mutate the balances
            var validAccounts = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, ct);

            foreach (var line in request.Lines)
            {
                if (!validAccounts.TryGetValue(line.AccountId, out var account))
                    return Result<int>.Failure($"Accounting Error: Account ID {line.AccountId} does not exist.");

                if (!account.IsTransactionAccount)
                    return Result<int>.Failure($"Accounting Error: Account '{account.Name}' is a Folder/Parent. You cannot post directly to it.");

                // UPDATE DOMAIN STATE: Standard Accounting Sign Convention
                // Debits increase asset/expense (positive). Credits increase liability/revenue/equity (negative).
                // Standard convention logic: Balance = Balance + Debit - Credit
                decimal movement = line.Debit - line.Credit;
                account.UpdateBalance(movement);
            }

            // =============================================================
            // RULE 4: AUTO-SEQUENCING (Thread-Safe)
            // =============================================================
            // Fetch the guaranteed unique sequence number via the Sequence Service
            string journalId = await _sequenceService.GenerateNextNumberAsync("JOURNAL", ct);

            // =============================================================
            // COMMIT TO DATABASE
            // =============================================================
            var journal = new JournalEntry
            {
                ReferenceNo = request.ReferenceNo, // Source Document (e.g., GRN-2024-001)
                Description = string.IsNullOrWhiteSpace(request.Description) ? $"JV Auto-Generated ({journalId})" : request.Description,
                Date = request.Date,
                Module = request.Module, // e.g., "Purchasing", "Sales"
                TotalAmount = totalDebits
                // Note: The actual JV number might be stored in a dedicated field later, 
                // but appending it to the description or relying on an Audit log is common.
            };

            foreach (var line in request.Lines)
            {
                journal.Lines.Add(new JournalLine
                {
                    AccountId = line.AccountId,
                    Debit = line.Debit,
                    Credit = line.Credit,
                    Description = string.IsNullOrWhiteSpace(line.Note) ? journal.Description : line.Note
                });
            }

            _context.JournalEntries.Add(journal);

            // The Accounts are automatically updated here because EF Core tracks the validAccounts dictionary.
            await _context.SaveChangesAsync(ct);

            return Result<int>.Success(journal.Id, $"Posted successfully: {journalId}");
        }
    }
}
