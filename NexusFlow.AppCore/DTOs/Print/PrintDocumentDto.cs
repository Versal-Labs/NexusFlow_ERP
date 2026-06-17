using System;
using System.Collections.Generic;

namespace NexusFlow.AppCore.DTOs.Print
{
    public class PrintDocumentDto
    {
        public string DocumentId { get; set; } = null!;
        public string DocumentType { get; set; } = null!;
        public string DocumentNumber { get; set; } = null!;
        public DateTime DocumentDate { get; set; }
        public string CustomerOrSupplierName { get; set; } = null!;
        public string BillingAddress { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        
        // Summary Totals
        public decimal SubTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string CurrencyCode { get; set; } = "USD";

        // Line Items
        public List<PrintLineItemDto> LineItems { get; set; } = new List<PrintLineItemDto>();
    }

    public class PrintLineItemDto
    {
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = null!;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "Pcs";
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
    }
}
