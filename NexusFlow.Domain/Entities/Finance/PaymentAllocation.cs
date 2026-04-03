using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Entities.Sales;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("PaymentAllocations", Schema = "Finance")]
    public class PaymentAllocation : AuditableEntity
    {
        public int PaymentTransactionId { get; set; }
        public PaymentTransaction PaymentTransaction { get; set; } = null!;

        public int? SalesInvoiceId { get; set; }
        public SalesInvoice? SalesInvoice { get; set; }

        // Used for Supplier Payments & Endorsements
        public int? SupplierBillId { get; set; }
        public SupplierBill? SupplierBill { get; set; }

        public decimal AmountAllocated { get; set; }
    }
}
