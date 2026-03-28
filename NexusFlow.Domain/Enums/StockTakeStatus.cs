using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum StockTakeStatus
    {
        Initiated = 1,   // The batch is created, waiting for floor staff to count
        Counted = 2,     // Floor staff submitted their blind counts
        Approved = 3,    // Manager approved variance -> GL and Stock Updated
        Rejected = 4     // Manager rejected the count (needs recount)
    }
}
