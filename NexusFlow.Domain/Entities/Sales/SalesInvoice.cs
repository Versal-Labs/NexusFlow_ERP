using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Sales
{
    [Table("SalesInvoices", Schema = "Sales")]
    public class SalesInvoice : AuditableEntity
    {
        // Document Info
        public string InvoiceNumber { get; set; } = string.Empty; // e.g., "INV-2024-001"
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }

        // Relationships
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        // Financials
        public decimal SubTotal { get; set; }       // Before Tax
        public decimal TotalTax { get; set; }       // VAT + SSCL
        public decimal TotalDiscount { get; set; }
        public decimal GrandTotal { get; set; }     // Final Payable

        // Status
        public bool IsPosted { get; set; } = false; // Draft vs Final

        public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
    }
}
