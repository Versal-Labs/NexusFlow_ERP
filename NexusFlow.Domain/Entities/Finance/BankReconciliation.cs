using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("BankReconciliations", Schema = "Finance")]
    public class BankReconciliation : AuditableEntity
    {
        public int Id { get; set; }

        public int BankAccountId { get; set; }
        public Account BankAccount { get; set; } = null!;

        public DateTime StatementDate { get; set; }
        public decimal StatementEndingBalance { get; set; }

        public bool IsFinalized { get; set; } = false;

        public ICollection<JournalLine> ClearedLines { get; set; } = new List<JournalLine>();
    }
}
