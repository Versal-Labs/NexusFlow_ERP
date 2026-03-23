using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Master
{
    public class ProductVariantDto
    {
        public int Id { get; set; }

        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;

        public decimal SellingPrice { get; set; }
        public decimal CostPrice { get; set; } // Standard Cost (The Legacy 'avar')
        public decimal MovingAverageCost { get; set; } // Added for operational reporting
        public decimal ReorderLevel { get; set; }
    }
}
