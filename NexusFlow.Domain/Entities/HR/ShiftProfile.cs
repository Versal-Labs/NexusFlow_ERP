using NexusFlow.Domain.Common;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.HR
{
    [Table("ShiftProfiles", Schema = "HR")]
    public class ShiftProfile : AuditableEntity
    {
        public string Name { get; set; } = string.Empty; // e.g., "Standard Morning Shift"

        public TimeSpan StartTime { get; set; } // e.g., 08:30
        public TimeSpan EndTime { get; set; }   // e.g., 17:30

        public int GracePeriodMinutes { get; set; } // e.g., 15 (Arrivals before 08:45 are not marked Late)
        public int HalfDayThresholdMinutes { get; set; } // Minimum hours to work to not be marked Absent

        // Navigation: Employees assigned to this shift
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }

    [Table("AttendanceLogs", Schema = "HR")]
    public class AttendanceLog : AuditableEntity
    {
        // This is the RAW dump from the fingerprint machine.
        // It is immutable. We never edit raw logs.
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }

        public DateTime PunchTime { get; set; }
        public string DeviceId { get; set; } = string.Empty; // Useful if you have multiple doors/machines

        // Some machines don't distinguish between IN/OUT, they just send a punch.
        // If your machine tells you, store it. Otherwise, our nightly job figures it out.
        public bool? IsCheckIn { get; set; }
    }

    [Table("DailyAttendanceRecords", Schema = "HR")]
    public class DailyAttendanceRecord : AuditableEntity
    {
        // This is the PROCESSED result calculated by Hangfire every night.
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }

        public DateTime Date { get; set; } // e.g., 2026-05-07

        public DateTime? FirstIn { get; set; }
        public DateTime? LastOut { get; set; }

        public AttendanceStatus Status { get; set; }

        public int LateMinutes { get; set; } = 0;
        public int OvertimeMinutes { get; set; } = 0;

        public string Remarks { get; set; } = string.Empty; // e.g., "Manual override by HR"
    }
}
