using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("JournalEntries", Schema = "Finance")]
    public class JournalEntry : AuditableEntity
    {
        public DateTime Date { get; set; }

        // e.g., "Sales Invoice #1001" or "Production Run #50"
        public string Description { get; set; } = string.Empty;

        // Which module generated this? (Sales, Inventory, Payroll)
        public string Module { get; set; } = string.Empty;

        // The ID of the source document (InvoiceId, ProductionId)
        public string ReferenceNo { get; set; } = string.Empty;

        public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();

        // Computed Total (Just for easier querying later)
        public decimal TotalAmount { get; set; }
    }
}
