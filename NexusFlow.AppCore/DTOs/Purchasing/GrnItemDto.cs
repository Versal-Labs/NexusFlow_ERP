using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Purchasing
{
    public class GrnItemDto
    {
        public int ProductVariantId { get; set; }
        public decimal QuantityReceived { get; set; }
        public decimal UnitCost { get; set; } // Allow override if price changed
    }
}
