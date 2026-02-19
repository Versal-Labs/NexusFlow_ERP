using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Services
{
    public class NumberSequenceService : INumberSequenceService
    {
        private readonly IErpDbContext _context;

        public NumberSequenceService(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateNextNumberAsync(string moduleName, CancellationToken ct)
        {
            // 1. Find the configuration row
            var sequence = await _context.NumberSequences
                .FirstOrDefaultAsync(x => x.Module == moduleName);

            if (sequence == null)
            {
                // Fallback or Error if config is missing
                throw new Exception($"Number sequence for '{moduleName}' not defined.");
            }

            // 2. Capture the current number to use
            long currentNum = sequence.NextNumber;

            // 3. Increment the number in the database for the next person
            sequence.NextNumber++;
            sequence.LastUsed = DateTime.UtcNow;

            // 4. Save immediately (Concurrency Handling is vital here in production)
            await _context.SaveChangesAsync(ct);

            // 5. Format and return
            // Matches your preview in screenshot: "SALES/1003" or "GRN-5000"
            // You might store a 'Separator' column in DB, here assuming '-' or '/' based on logic
            string separator = sequence.Module == "Sales" ? "/" : "-";
            return $"{sequence.Prefix}{separator}{currentNum}";
        }
    }
}
