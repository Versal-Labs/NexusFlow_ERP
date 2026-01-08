using NexusFlow.Domain.Common;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("Accounts", Schema = "Finance")] // <--- THIS IS THE SCHEMA DEFINITION
    public class Account : AuditableEntity
    {
        // The "GL Code" (e.g., "1001" or "50-100")
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty; // e.g., "Cash in Hand"

        public AccountType Type { get; set; } // Asset, Liability, etc.

        // If TRUE, you can select this in an invoice. 
        // If FALSE, it is just a folder (like "Current Assets").
        public bool IsTransactionAccount { get; set; }

        // =========================================================
        // SELF-REFERENCING RELATIONSHIP (The Tree)
        // =========================================================
        public int? ParentAccountId { get; set; }
        public Account? ParentAccount { get; set; }

        public ICollection<Account> ChildAccounts { get; set; } = new List<Account>();

        // =========================================================
        // FINANCIAL DATA
        // =========================================================
        // The current running balance. 
        // Positive for Debits (Assets/Exp), Negative for Credits (Liab/Rev) usually,
        // OR store absolute value and manage sign in logic. 
        // Standard ERPs often store Debits positive, Credits negative.
        public decimal Balance { get; private set; } = 0;

        public void UpdateBalance(decimal amount)
        {
            Balance += amount;
        }
    }
}
