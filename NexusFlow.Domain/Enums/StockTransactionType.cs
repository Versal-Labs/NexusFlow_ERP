using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum StockTransactionType
    {
        PurchaseIn = 1,    // GRN
        SalesOut = 2,      // Invoice
        Adjustment = 3,    // Stock Take
        TransferOut = 4,   // Sending to Factory (Reduces Main)
        TransferIn = 5,    // Receiving at Factory (Increases Factory)
        ProductionOut = 6, // Consumed raw material
        ProductionIn = 7   // Finished good created
    }
}
