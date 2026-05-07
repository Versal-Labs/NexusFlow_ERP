using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.HR;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Payroll
{
    [Table("SalaryAdvances", Schema = "Payroll")]
    public class SalaryAdvance : AuditableEntity
    {
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }

        public decimal Amount { get; set; }
        public DateTime RequestDate { get; set; }
        public string Reason { get; set; } = string.Empty;

        // The exact month this should be deducted (Format: "YYYY-MM", e.g., "2026-05")
        public string DeductionMonth { get; set; } = string.Empty;

        public bool IsDeducted { get; set; } = false; // Hangfire marks this true when payroll is posted
    }

    [Table("EmployeeLoans", Schema = "Payroll")]
    public class EmployeeLoan : AuditableEntity
    {
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }

        public decimal PrincipalAmount { get; set; }
        public decimal InterestRatePercentage { get; set; } // E.g., 0.00 for interest-free company loans
        public int TermInMonths { get; set; }

        // Equated Monthly Installment (Calculated when loan is approved)
        public decimal EMIAmount { get; set; }

        public DateTime DisbursementDate { get; set; }
        public LoanStatus Status { get; set; } = LoanStatus.Active;

        public ICollection<LoanRepaymentSchedule> RepaymentSchedules { get; set; } = new List<LoanRepaymentSchedule>();
    }

    [Table("LoanRepaymentSchedules", Schema = "Payroll")]
    public class LoanRepaymentSchedule : AuditableEntity
    {
        public int EmployeeLoanId { get; set; }
        public EmployeeLoan EmployeeLoan { get; set; }

        public int InstallmentNumber { get; set; } // 1, 2, 3...

        // The month this installment is due (Format: "YYYY-MM")
        public string TargetMonth { get; set; } = string.Empty;

        public decimal PrincipalComponent { get; set; }
        public decimal InterestComponent { get; set; }
        public decimal TotalInstallment { get; set; } // Principal + Interest

        // The Payroll engine looks for IsPaid == false and TargetMonth == CurrentPayrollMonth
        public bool IsPaid { get; set; } = false;
    }
}
