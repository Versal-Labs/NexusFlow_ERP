using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Purchasing
{
    [Table("GoodsReceipts", Schema = "Purchasing")]
    public class GoodsReceipt : AuditableEntity
    {
        public int Id { get; set; }
        public string GrnNumber { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = null!;

        public string ReferenceNo { get; set; } = string.Empty; // e.g., Supplier Delivery Note
        public string Remarks { get; set; } = string.Empty;

        public bool IsPosted { get; set; }
        public decimal TotalValue { get; set; }

        public ICollection<GoodsReceiptItem> Items { get; set; } = new List<GoodsReceiptItem>();
    }
}
