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

        // Extra document-specific scalar fields, for example EmployeeCode or PayPeriod.
        public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // Line Items
        public List<PrintLineItemDto> LineItems { get; set; } = new List<PrintLineItemDto>();

        // Named repeating sections used by document-specific Word templates.
        public Dictionary<string, List<PrintTableRowDto>> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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

    public class PrintTableRowDto
    {
        public string ItemCode { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string ReferenceDate { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
        public decimal Amount { get; set; }
        public decimal Earnings { get; set; }
        public decimal Deductions { get; set; }
        public string Remarks { get; set; } = string.Empty;

        public static PrintTableRowDto FromLineItem(PrintLineItemDto item)
        {
            return new PrintTableRowDto
            {
                ItemCode = item.ItemCode,
                ReferenceNumber = item.ItemCode,
                Description = item.Description,
                Quantity = item.Quantity,
                Unit = item.Unit,
                UnitPrice = item.UnitPrice,
                Discount = item.Discount,
                TaxAmount = item.TaxAmount,
                LineTotal = item.LineTotal,
                Amount = item.LineTotal
            };
        }
    }
}
