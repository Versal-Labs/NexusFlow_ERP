using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.DTOs.Sales
{
    public class CommissionRuleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public CommissionRuleType RuleType { get; set; }
        public string? RuleTypeName => RuleType.ToString();

        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }

        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; } // Displayed as "[Code] First Last"

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        public decimal CommissionPercentage { get; set; }
        public bool IsActive { get; set; }
    }
}
