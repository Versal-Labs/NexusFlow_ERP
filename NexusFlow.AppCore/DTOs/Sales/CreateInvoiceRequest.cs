using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Sales
{
    public class CreateInvoiceRequest
    {
        public DateTime Date { get; set; }
        public DateTime DueDate { get; set; }
        public int CustomerId { get; set; }
        public int WarehouseId { get; set; }
        public int? SalesRepId { get; set; }
        public string CustomerPoNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public bool ApplyVat { get; set; }
        public bool IsDraft { get; set; }
        public decimal GlobalDiscountAmount { get; set; }

        public List<InvoiceLineDto> Items { get; set; } = new();
    }

    public class InvoiceLineDto
    {
        public int ProductVariantId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; } // Absolute amount per line
    }
}
