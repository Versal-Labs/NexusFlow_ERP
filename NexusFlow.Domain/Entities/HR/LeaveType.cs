using NexusFlow.Domain.Common;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.HR
{
    [Table("LeaveTypes", Schema = "HR")]
    public class LeaveType : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // Annual, Casual, Medical
        public bool IsPaid { get; set; } = true;
        public int MaxDaysPerYear { get; set; }
    }

    [Table("LeaveRequests", Schema = "HR")]
    public class LeaveRequest : AuditableEntity
    {
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }

        public int LeaveTypeId { get; set; }
        public LeaveType LeaveType { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalDays { get; set; } // e.g., 1.5 days

        public string Reason { get; set; } = string.Empty;
        public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;

        public int? ApprovedById { get; set; } // Links to the Manager/HR who clicked Approve
        public Employee? ApprovedBy { get; set; }
    }
}
