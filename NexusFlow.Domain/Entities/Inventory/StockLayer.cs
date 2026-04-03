using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Inventory
{
    [Table("StockLayers", Schema = "Inventory")]
    public class StockLayer : AuditableEntity
    {
        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; }

        // FIFO DATA
        public string BatchNo { get; set; } = string.Empty;
        public DateTime DateReceived { get; set; }

        public decimal UnitCost { get; set; }
        public decimal RemainingQty { get; set; }
        public decimal InitialQty { get; set; }

        // ARCHITECTURAL ADDITION: Critical for FIFO Query Optimization
        public bool IsExhausted { get; set; } = false;

        // ========================================================
        // ARCHITECTURAL ADDITION: Computed Properties
        // ========================================================

        // This is your 'costval' / 'totcost'. 
        // NotMapped ensures it doesn't create a useless column in SQL.
        [NotMapped]
        public decimal CurrentTotalValue => RemainingQty * UnitCost;

        [NotMapped]
        public decimal InitialTotalValue => InitialQty * UnitCost;
    }
}
