using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.HR;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Sales
{
    [Table("CommissionRules", Schema = "Sales")]
    public class CommissionRule : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "Standard Base 3%", "John's Denim Override"

        public CommissionRuleType RuleType { get; set; }

        // ==========================================
        // DIMENSION 1: THE "WHAT" (Product Scope)
        // ==========================================
        // Nullable! If RuleType == GlobalFlatRate, this is left null.
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        // ==========================================
        // DIMENSION 2: THE "WHO" (Employee Scope)
        // ==========================================
        // Nullable! If null, this rule applies to ALL Sales Reps.
        // If set, it applies ONLY to this specific Rep.
        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        // ==========================================
        // DIMENSION 3: THE "WHEN" (Temporal Scope)
        // ==========================================
        // Nullable! Allows admins to set temporary promotional rates.
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        // The actual mathematical rate (e.g., 5.00 for 5%)
        public decimal CommissionPercentage { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
