using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.HR;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Sales
{
    [Table("CommissionLedger", Schema = "Sales")]
    public class CommissionLedger : AuditableEntity
    {
        public int SalesRepId { get; set; }
        public Employee SalesRep { get; set; }

        public int SalesInvoiceId { get; set; }
        public SalesInvoice SalesInvoice { get; set; }

        public decimal CommissionAmount { get; set; }
        public CommissionStatus Status { get; set; } = CommissionStatus.Unearned;
    }
}
