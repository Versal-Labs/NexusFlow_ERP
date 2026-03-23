using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Sales
{
    [Table("SalesOrderItems", Schema = "Sales")]
    public class SalesOrderItem : AuditableEntity
    {
        public int SalesOrderId { get; set; }
        public SalesOrder SalesOrder { get; set; }

        public int ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal LineTotal { get; set; }
    }
}
