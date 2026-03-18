using NexusFlow.Domain.Common;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Purchasing
{
    [Table("SupplierBills", Schema = "Purchasing")]
    public class SupplierBill : AuditableEntity
    {
        public string BillNumber { get; set; } = string.Empty;
        public string SupplierInvoiceNo { get; set; } = string.Empty; // The vendor's physical invoice number
        public DateTime BillDate { get; set; }
        public DateTime DueDate { get; set; }

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        public string Remarks { get; set; } = string.Empty;
        public bool ApplyVat { get; set; }
        public bool IsPosted { get; set; }

        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }

        public decimal AmountPaid { get; set; } = 0;
        public InvoicePaymentStatus PaymentStatus { get; set; } = InvoicePaymentStatus.Unpaid;

        public ICollection<SupplierBillItem> Items { get; set; } = new List<SupplierBillItem>();
        public ICollection<GRN> Grns { get; set; } = new List<GRN>();
    }
}
