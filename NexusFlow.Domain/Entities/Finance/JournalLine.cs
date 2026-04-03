using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("JournalLines", Schema = "Finance")]
    public class JournalLine : AuditableEntity
    {
        public int JournalEntryId { get; set; }
        public JournalEntry JournalEntry { get; set; }

        public int AccountId { get; set; }
        public Account Account { get; set; }

        // Standard Accounting: Store both, but one is usually 0
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public bool IsCleared { get; set; } = false;
        public int? BankReconciliationId { get; set; }
        public BankReconciliation? BankReconciliation { get; set; }

        // Optional: Dimension tagging (e.g., Cost Center, Department)
        public string? Description { get; set; }
    }
}
