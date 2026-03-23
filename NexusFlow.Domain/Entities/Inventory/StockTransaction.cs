using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Inventory
{
    [Table("StockTransactions", Schema = "Inventory")]
    public class StockTransaction : AuditableEntity
    {
        public DateTime Date { get; set; }

        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; }

        public StockTransactionType Type { get; set; }

        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }

        public string ReferenceDocNo { get; set; } = string.Empty;

        // ARCHITECTURAL ADDITION: Critical for Auditing manual adjustments and cutovers
        public string Notes { get; set; } = string.Empty;
    }
}
