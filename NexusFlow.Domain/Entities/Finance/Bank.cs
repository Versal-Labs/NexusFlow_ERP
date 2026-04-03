using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("Banks", Schema = "Finance")]
    public class Bank : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;       // e.g., "Bank of Ceylon"
        public string BankCode { get; set; } = string.Empty;   // e.g., "7010"
        public string? SwiftCode { get; set; }                 // e.g., "BCEYLKLX"
        public string Type { get; set; } = string.Empty;       // e.g., "Licensed Commercial Bank"
        public bool IsActive { get; set; } = true;

        public ICollection<BankBranch> Branches { get; set; } = new List<BankBranch>();
    }
}
