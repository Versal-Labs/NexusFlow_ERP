using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Sales
{
    public class CreateInvoiceRequest
    {
        public int CustomerId { get; set; }
        public DateTime Date { get; set; }
        public DateTime DueDate { get; set; }

        // Which warehouse are we selling from? (e.g., Showroom)
        public int WarehouseId { get; set; }

        public List<InvoiceLineDto> Items { get; set; } = new();
    }

    public class InvoiceLineDto
    {
        public int ProductVariantId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; } // The user might override the standard price
        public decimal Discount { get; set; }
    }
}
