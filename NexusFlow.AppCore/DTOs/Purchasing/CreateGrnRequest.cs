using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Purchasing
{
    public class CreateGrnRequest
    {
        public DateTime ReceiptDate { get; set; }
        public int SupplierId { get; set; }
        public int WarehouseId { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public List<CreateGrnItemRequest> Items { get; set; } = new();
    }

    public class CreateGrnItemRequest
    {
        public int ProductVariantId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }
}
