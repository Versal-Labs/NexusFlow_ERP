using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IJournalService
    {
        /// <summary>
        /// Validates and Posts a Transaction to the General Ledger.
        /// Throws validation error if Debits != Credits.
        /// </summary>
        Task<Result<int>> PostJournalAsync(JournalEntryRequest request);
    }
}
