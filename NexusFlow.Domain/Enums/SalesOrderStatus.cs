using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum SalesOrderStatus
    {
        Draft = 1,       // Rep is still building the order
        Submitted = 2,   // Sent to Back-Office for review
        Converted = 3,   // Back-Office approved and converted to SalesInvoice
        Cancelled = 4,
    }
}
