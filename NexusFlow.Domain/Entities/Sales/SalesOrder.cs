using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.HR;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Sales
{
    [Table("SalesOrders", Schema = "Sales")]
    public class SalesOrder : AuditableEntity
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        public int SalesRepId { get; set; } // Linked to HR.Employee
        public Employee SalesRep { get; set; }

        public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;

        public decimal TotalAmount { get; set; }
        public string Notes { get; set; } = string.Empty;

        public ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
    }
}
