using NexusFlow.Domain.Common;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("Accounts", Schema = "Finance")]
    public class Account : AuditableEntity
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AccountType Type { get; set; }

        public bool IsTransactionAccount { get; set; }
        public int? ParentAccountId { get; set; }
        public Account? ParentAccount { get; set; }
        public ICollection<Account> ChildAccounts { get; set; } = new List<Account>();

        // Financial Balance (Base Currency: LKR)
        public decimal Balance { get; private set; } = 0;

        // --- NEW LIFECYCLE & CONTROL FLAGS ---
        public bool IsActive { get; set; } = true;
        public bool IsSystemAccount { get; set; } = false;
        public bool RequiresReconciliation { get; set; } = false;

        public void UpdateBalance(decimal amount)
        {
            Balance += amount;
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}
