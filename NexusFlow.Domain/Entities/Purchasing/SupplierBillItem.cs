using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Purchasing
{
    [Table("SupplierBillItems", Schema = "Purchasing")]
    public class SupplierBillItem : BaseEntity
    {
        public int SupplierBillId { get; set; }
        public SupplierBill SupplierBill { get; set; } = null!;

        // A line can EITHER be a Product (clearing GRN) OR a Direct Expense (e.g., Freight)
        public int? ProductVariantId { get; set; }
        public ProductVariant? ProductVariant { get; set; }

        public int? ExpenseAccountId { get; set; }
        public Account? ExpenseAccount { get; set; }

        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
