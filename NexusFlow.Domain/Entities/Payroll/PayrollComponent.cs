using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.HR;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Payroll
{
    [Table("PayrollComponents", Schema = "Payroll")]
    public class PayrollComponent : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "Food Allowance", "Late Deduction"

        public PayrollComponentType Type { get; set; }
        public CalculationType CalculationType { get; set; }

        // The default value. (e.g., 500 for PerAttendanceDay, or 10.00 for PercentageOfBasic)
        public decimal DefaultRate { get; set; }

        // Sri Lankan Statutory Flags (CRITICAL)
        public bool IsEPFCalculable { get; set; } = false; // Does this allowance add to the EPF base?
        public bool IsETFCalculable { get; set; } = false;
        public bool IsTaxable { get; set; } = true;        // Does this count towards APIT/PAYE?

        public bool IsActive { get; set; } = true;

        // GL Integration (Automated Journal Posting)
        public int? DebitAccountId { get; set; }  // E.g., "Food Allowance Expense Account"
        public Account? DebitAccount { get; set; }

        public int? CreditAccountId { get; set; } // E.g., "Accrued Allowances Payable"
        public Account? CreditAccount { get; set; }
    }

    [Table("EmployeePayrollComponents", Schema = "Payroll")]
    public class EmployeePayrollComponent : AuditableEntity
    {
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }

        public int PayrollComponentId { get; set; }
        public PayrollComponent PayrollComponent { get; set; }

        // Allows HR to override the global default rate for a specific employee.
        // If null, the system uses PayrollComponent.DefaultRate.
        public decimal? OverrideRate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
