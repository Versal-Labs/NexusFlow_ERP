using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum PurchaseOrderStatus
    {
        Draft = 1,
        Approved = 2,
        Received = 3, // Fully received via GRN
        Closed = 4,
        Cancelled = 5,
        Partial = 6
    }
}
