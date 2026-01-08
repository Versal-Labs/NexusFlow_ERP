using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Master
{
    [Table("BomComponents", Schema = "Master")]
    public class BomComponent : AuditableEntity
    {
        public int BillOfMaterialId { get; set; }
        public BillOfMaterial BillOfMaterial { get; set; }

        // The Ingredient (e.g., Fabric Roll - Blue)
        public int MaterialVariantId { get; set; }
        public ProductVariant MaterialVariant { get; set; }

        // Quantity Needed (e.g., 1.5)
        public decimal Quantity { get; set; }

        // UOM is derived from the MaterialVariant's Product
        // Cost is derived from the StockLayer (FIFO) during production
    }
}
