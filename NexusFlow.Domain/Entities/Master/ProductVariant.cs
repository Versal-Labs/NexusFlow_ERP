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

        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;  // CSV: itemcode
        public string? Barcode { get; set; }

        public string Size { get; set; } = string.Empty;  // CSV: msize / size
        public string Color { get; set; } = string.Empty; // CSV: color

        // Pricing & Valuation
        public decimal CostPrice { get; set; }         // Standard/Last Purchase Cost
        public decimal MovingAverageCost { get; set; } // The Client's Request (CSV: avar)
        public decimal SellingPrice { get; set; }      // CSV: selprice
        public decimal MinimumSellingPrice { get; set; } // CSV: minsellpri

        public decimal ReorderLevel { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
