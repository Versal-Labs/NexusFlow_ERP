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

        public JournalService(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<int>> PostJournalAsync(JournalEntryRequest request)
        {
            // =============================================================
            // RULE 1: PERIOD LOCKING (Prevent Back-dating)
            // =============================================================
            var period = await _context.FinancialPeriods
                .FirstOrDefaultAsync(p => request.Date >= p.StartDate && request.Date <= p.EndDate);

            if (period != null && period.IsClosed)
            {
                return Result<int>.Failure($"Accounting Error: The Financial Period '{period.Name}' is CLOSED. You cannot post transactions to this date.");
            }
            // (Optional: You can also fail if no period exists at all)

            // =============================================================
            // RULE 2: ACCOUNT VALIDATION (Integrity Check)
            // =============================================================
            var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();
            var validAccounts = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id);

            foreach (var line in request.Lines)
            {
                if (!validAccounts.ContainsKey(line.AccountId))
                    return Result<int>.Failure($"Accounting Error: Account ID {line.AccountId} does not exist.");

                // Standard: You cannot post to a "Parent/Folder" account, only transaction accounts
                if (!validAccounts[line.AccountId].IsTransactionAccount)
                    return Result<int>.Failure($"Accounting Error: Account '{validAccounts[line.AccountId].Name}' is a Folder/Parent. You cannot post directly to it.");
            }

            // =============================================================
            // RULE 3: STRICT DOUBLE ENTRY (The Golden Rule)
            // =============================================================
            decimal totalDebits = request.Lines.Sum(x => x.Debit);
            decimal totalCredits = request.Lines.Sum(x => x.Credit);

            if (Math.Abs(totalDebits - totalCredits) > 0.00m) // Tolerance for floating point
            {
                return Result<int>.Failure($"Double Entry Violation: Debits ({totalDebits:N2}) != Credits ({totalCredits:N2}). Difference: {totalDebits - totalCredits}");
            }

            if (totalDebits == 0) return Result<int>.Failure("Transaction cannot be zero.");

            // =============================================================
            // RULE 4: AUTO-SEQUENCING (Audit Trail)
            // =============================================================
            // Format: JV-{Year}-{Sequence} (e.g., JV-2024-0005)
            // Note: In high-traffic systems, use a dedicated Sequence Table or Database Sequence.
            // For this simplified version, we count existing entries for the year.

            string yearPrefix = $"JV-{request.Date.Year}-";

            // This is a simplified "Last Number" logic. 
            // Real ERPs use a locked sequence table to prevent race conditions.
            var lastEntry = await _context.JournalEntries
                .Where(j => j.ReferenceNo.StartsWith(yearPrefix))
                .OrderByDescending(j => j.ReferenceNo)
                .Select(j => j.ReferenceNo)
                .FirstOrDefaultAsync();

            int nextNum = 1;
            if (lastEntry != null)
            {
                string numPart = lastEntry.Substring(yearPrefix.Length);
                if (int.TryParse(numPart, out int lastNum)) nextNum = lastNum + 1;
            }

            string journalId = $"{yearPrefix}{nextNum:D6}"; // JV-2024-000001

            // =============================================================
            // COMMIT
            // =============================================================
            var journal = new JournalEntry
            {
                ReferenceNo = journalId, // The Official Accounting ID
                Description = request.Description,
                Date = request.Date,
                Module = request.Module, // "Inventory", "Sales"
                TotalAmount = totalDebits,
                // Store the "Source Document" reference separately (e.g., Invoice #)
                // Note: You might want to add a property 'SourceRef' to JournalEntry entity for this.
            };

            foreach (var line in request.Lines)
            {
                journal.Lines.Add(new JournalLine
                {
                    AccountId = line.AccountId,
                    Debit = line.Debit,
                    Credit = line.Credit,
                    Description = line.Note
                });
            }

            _context.JournalEntries.Add(journal);
            await _context.SaveChangesAsync(CancellationToken.None);

            return Result<int>.Success(journal.Id, $"Posted successfully: {journalId}");
        }
    }
}
