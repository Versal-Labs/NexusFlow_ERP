using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Purchasing
{
    [Table("GRNs", Schema = "Purchasing")]
    public class GRN : AuditableEntity
    {
        public string GrnNumber { get; set; } = string.Empty; // e.g., "GRN-2024-001"
        public DateTime ReceivedDate { get; set; }

        public int PurchaseOrderId { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; }

        public int WarehouseId { get; set; } // Where did we store the fabric?
        public string SupplierInvoiceNo { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public bool IsBilled { get; set; } = false;

        public int? SupplierBillId { get; set; }
        public SupplierBill? SupplierBill { get; set; }

        public ICollection<GRNItem> Items { get; set; } = new List<GRNItem>();
    }
}
