using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Entities.Sales
{
    public class CreditNote : AuditableEntity
    {
        public int Id { get; set; }
        public string CreditNoteNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        public int SalesInvoiceId { get; set; } // The invoice being reversed
        public SalesInvoice SalesInvoice { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        public int ReturnToWarehouseId { get; set; } // Ideally a Quarantine Warehouse

        public string Reason { get; set; } = string.Empty;

        public decimal SubTotal { get; set; }
        public decimal TotalTax { get; set; }
        public decimal GrandTotal { get; set; }

        public bool IsPosted { get; set; }

        public ICollection<CreditNoteItem> Items { get; set; } = new List<CreditNoteItem>();
    }
}
