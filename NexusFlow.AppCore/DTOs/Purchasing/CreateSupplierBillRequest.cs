using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Purchasing
{
    public class CreateSupplierBillRequest
    {
        public DateTime BillDate { get; set; }
        public DateTime DueDate { get; set; }
        public int SupplierId { get; set; }
        public string SupplierInvoiceNo { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public bool ApplyVat { get; set; }
        public bool IsDraft { get; set; }
        public List<int> LinkedGrnIds { get; set; } = new();

        public List<CreateSupplierBillItemRequest> Items { get; set; } = new();
    }

    public class CreateSupplierBillItemRequest
    {
        public int? ProductVariantId { get; set; }
        public int? ExpenseAccountId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
