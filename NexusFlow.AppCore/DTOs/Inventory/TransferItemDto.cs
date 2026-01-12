using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Inventory
{
    public class TransferItemDto
    {
        public int ProductVariantId { get; set; }
        public decimal Qty { get; set; }
    }
}
