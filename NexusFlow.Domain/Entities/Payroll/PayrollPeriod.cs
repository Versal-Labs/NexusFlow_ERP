using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.HR;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Payroll
{
    public enum PayrollPeriodStatus { Draft = 1, PendingApproval = 2, Posted = 3, Paid = 4 }

    [Table("PayrollPeriods", Schema = "Payroll")]
    public class PayrollPeriod : AuditableEntity
    {
        public string MonthYear { get; set; } = string.Empty; // e.g., "2026-05"
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public PayrollPeriodStatus Status { get; set; } = PayrollPeriodStatus.Draft;

        public ICollection<PayrollSlip> Slips { get; set; } = new List<PayrollSlip>();
    }

    [Table("PayrollSlips", Schema = "Payroll")]
    public class PayrollSlip : AuditableEntity
    {
        public int PayrollPeriodId { get; set; }
        public PayrollPeriod PayrollPeriod { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }

        // Totals
        public decimal GrossBasic { get; set; }
        public decimal TotalAllowances { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetPay { get; set; }

        // Statutory Employer Contributions (Not deducted from employee, but company pays)
        public decimal EmployerEPF { get; set; }
        public decimal EmployerETF { get; set; }

        public ICollection<PayrollSlipLineItem> LineItems { get; set; } = new List<PayrollSlipLineItem>();
    }

    [Table("PayrollSlipLineItems", Schema = "Payroll")]
    public class PayrollSlipLineItem : AuditableEntity
    {
        public int PayrollSlipId { get; set; }
        public PayrollSlip PayrollSlip { get; set; }

        public string Description { get; set; } = string.Empty; // e.g., "Food Allowance", "EPF 8%", "Loan EMI"
        public decimal Amount { get; set; }

        // 1 = Addition (Allowance/Basic), 2 = Deduction
        public int Type { get; set; }
    }
}
