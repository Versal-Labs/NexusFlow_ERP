using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Inventory
{
    [Table("StockTakeItem", Schema = "Inventory")]
    public class StockTakeItem
    {
        public int Id { get; set; }

        public int StockTakeId { get; set; }
        public StockTake StockTake { get; set; }

        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        // The Snapshot taken at the moment of Initiation
        public decimal SystemQty { get; set; }

        // What the floor worker actually typed in
        public decimal? CountedQty { get; set; }

        // CountedQty - SystemQty (e.g., if System says 10, Count is 8, Variance is -2)
        public decimal VarianceQty { get; set; }

        // The FIFO unit cost at the time of the count (to value the shrinkage)
        public decimal UnitCost { get; set; }

        // VarianceQty * UnitCost (e.g., -2 * $15 = -$30.00 Shrinkage)
        public decimal VarianceValue { get; set; }
    }
}
