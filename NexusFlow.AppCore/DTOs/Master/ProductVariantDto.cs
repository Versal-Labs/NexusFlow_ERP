using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Master
{
    public class ProductVariantDto
    {
        // If ID is 0, it's a new variant. If > 0, it's an update.
        public int Id { get; set; }

        // e.g., "L", "XL"
        public string Size { get; set; } = string.Empty;

        // e.g., "Red", "Blue"
        public string Color { get; set; } = string.Empty;

        // e.g., "SHIRT-RED-L"
        public string SKU { get; set; } = string.Empty;

        public decimal SellingPrice { get; set; }
        public decimal CostPrice { get; set; } // Standard Cost
        public decimal ReorderLevel { get; set; }
    }
}
