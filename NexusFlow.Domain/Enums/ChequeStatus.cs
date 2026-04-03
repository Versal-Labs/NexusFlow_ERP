using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum ChequeStatus
    {
        InSafe = 0,      // Physical cheque is sitting in the company safe
        Deposited = 1,   // Sent to the bank, waiting for clearance
        Endorsed = 2,    // Swapped/Given to a supplier to pay an AP Bill
        Cleared = 3,     // Bank reconciliation confirmed the money is real
        Bounced = 4      // Dishonored. Money reversed. Customer owes us again.
    }
}
