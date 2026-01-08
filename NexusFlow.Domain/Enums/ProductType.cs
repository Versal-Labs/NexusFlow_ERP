using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum ProductType
    {
        RawMaterial = 1,   // Fabric, Buttons, Zippers (Bought, consumed)
        FinishedGood = 2,  // Jeans, Shirts (Manufactured, sold)
        Service = 3,       // Sewing Charge, Delivery Fee (Non-stock)
        WorkInProgress = 4 // Intermediate bundles (Optional, but good for tracking)
    }
}
