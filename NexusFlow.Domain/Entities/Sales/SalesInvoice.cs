using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;
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
        public int? SalesRepId { get; set; } // For Commissions
        public string Notes { get; set; } = string.Empty;
        public bool ApplyVat { get; set; } = true;

        public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();

        public decimal AmountPaid { get; set; } = 0;
        public InvoicePaymentStatus PaymentStatus { get; set; } = InvoicePaymentStatus.Unpaid;
        public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
    }
}
