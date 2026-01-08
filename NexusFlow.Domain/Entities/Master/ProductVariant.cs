using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("ProductVariants", Schema = "Master")]
    public class ProductVariant : AuditableEntity
    {
        public int ProductId { get; set; }
        public Product Product { get; set; }

        // The Specifics
        public string Name { get; set; } = string.Empty; // Auto-generated: "Oxford Shirt - L - Red"
        public string SKU { get; set; } = string.Empty;  // Unique Barcode: "SHIRT-RED-L-001"

        public string Size { get; set; } = string.Empty; // "L", "XL", "42"
        public string Color { get; set; } = string.Empty; // "Red", "Blue"

        // Pricing (Standard)
        public decimal CostPrice { get; set; }    // Standard Cost (Reference only, FIFO uses StockLayer)
        public decimal SellingPrice { get; set; } // Wholesale Price

        // Re-Order Logic
        public decimal ReorderLevel { get; set; } // Alert when stock < 10
    }
}
