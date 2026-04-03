using NexusFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Finance
{
    [Table("BankBranches", Schema = "Finance")]
    public class BankBranch : AuditableEntity
    {
        public int BankId { get; set; }
        public Bank Bank { get; set; } = null!;

        public string BranchCode { get; set; } = string.Empty; // e.g., "001"
        public string BranchName { get; set; } = string.Empty; // e.g., "Head Office"
        public bool IsActive { get; set; } = true;
    }
}
