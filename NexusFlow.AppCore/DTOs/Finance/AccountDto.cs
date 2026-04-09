using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Finance
{
    public class AccountDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // String representation of Enum
        public bool IsTransactionAccount { get; set; }
        public decimal Balance { get; set; }

        public bool IsActive { get; set; }
        public bool IsSystemAccount { get; set; }
        public bool RequiresReconciliation { get; set; }

        // For Tree Structure
        public int? ParentAccountId { get; set; }
        public List<AccountDto> Children { get; set; } = new();
    }
}
