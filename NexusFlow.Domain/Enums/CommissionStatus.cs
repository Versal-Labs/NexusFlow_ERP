using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum CommissionStatus
    {
        Unearned = 1,          // Invoice created, cash not received
        PendingClearance = 2,  // Paid via Cheque, waiting for bank clearance
        ReadyToPay = 3,        // Cash received / Cheque cleared. Ready for Payroll.
        Paid = 4               // Processed in Payroll
    }
}
