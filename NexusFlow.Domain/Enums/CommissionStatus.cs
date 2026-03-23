using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum CommissionStatus
    {
        Unearned = 1,    // Invoice created, but customer hasn't paid yet
        ReadyToPay = 2,  // Customer paid, waiting for Payroll sweep
        Paid = 3         // Included in a finalized Payroll run
    }
}
