using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Sales
{
    [Table("SalesInvoiceItems", Schema = "Sales")]
    public class SalesInvoiceItem : AuditableEntity
    {
        public int SalesInvoiceId { get; set; }
        public SalesInvoice SalesInvoice { get; set; }

        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        public string Description { get; set; } = string.Empty; // Snapshot of product name

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; } // Selling Price
        public decimal Discount { get; set; }

        public decimal LineTotal { get; set; } // (Qty * Price) - Discount
    }
}
