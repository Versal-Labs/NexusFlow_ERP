using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Purchasing
{
    [Table("PurchaseOrders", Schema = "Purchasing")]
    public class PurchaseOrder : AuditableEntity
    {
        public string PoNumber { get; set; } = string.Empty; // e.g., "PO-2024-001"
        public DateTime Date { get; set; }
        public DateTime? ExpectedDate { get; set; }

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }

        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

        public decimal TotalAmount { get; set; }
        public string Note { get; set; }

        public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
    }

    [Table("PurchaseOrderItems", Schema = "Purchasing")]
    public class PurchaseOrderItem : AuditableEntity
    {
        public int PurchaseOrderId { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; }

        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        public decimal QuantityOrdered { get; set; }
        public decimal UnitCost { get; set; } // Agreed Price

        // Helper to track how much arrived so far
        public decimal QuantityReceived { get; set; }
    }
}
