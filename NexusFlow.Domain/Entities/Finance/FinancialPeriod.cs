using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("FinancialPeriods", Schema = "Finance")]
    public class FinancialPeriod : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "January 2024"
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // If TRUE, the Engine rejects any transaction with a date in this range.
        public bool IsClosed { get; set; }
    }
}
