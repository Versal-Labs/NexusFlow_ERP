using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.System;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.HR
{
    [Table("Employees", Schema = "HR")]
    public class Employee : AuditableEntity
    {
        // 1. Identity
        public string EmployeeCode { get; set; } = string.Empty; // e.g., EMP-1001
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty; // Acts as Username
        public string Phone { get; set; } = string.Empty;
        public string NIC { get; set; } = string.Empty; // National ID

        // 2. Payroll / HR Hooks
        public decimal BasicSalary { get; set; }
        public string EPF_No { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string BankAccountNo { get; set; } = string.Empty;

        // 3. Operational Roles
        public bool IsSalesRep { get; set; }

        // 4. System Access (Linked to ASP.NET Identity)
        public string? ApplicationUserId { get; set; }

        [ForeignKey("ApplicationUserId")]
        public ApplicationUser? ApplicationUser { get; set; }
    }
}
