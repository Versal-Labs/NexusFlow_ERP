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
        // WHICH Item?
        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        // WHERE is it? (Main Store vs Factory)
        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; }

        // FIFO DATA
        public string BatchNo { get; set; } = string.Empty; // e.g., "GRN-1001"
        public DateTime DateReceived { get; set; }

        // The Cost per Meter at the time of purchase
        public decimal UnitCost { get; set; }

        // Current Quantity in this specific pile
        public decimal RemainingQty { get; set; }

        public decimal InitialQty { get; set; } // For audit trails
    }
}
