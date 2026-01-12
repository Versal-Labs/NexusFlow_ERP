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

        // Logic: In, Out, Transfer
        public StockTransactionType Type { get; set; }

        public decimal Qty { get; set; }

        // The Cost Value of this specific movement (calculated via FIFO)
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }

        // Traceability: "GRN-1001" or "INV-500" or "TRF-200"
        public string ReferenceDocNo { get; set; } = string.Empty;
    }
}
