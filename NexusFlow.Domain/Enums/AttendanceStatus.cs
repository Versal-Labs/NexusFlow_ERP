using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Domain.Enums
{
    public enum AttendanceStatus
    {
        Present = 1,
        Absent = 2,
        HalfDay = 3,
        OnLeave = 4,
        RestDay = 5,   // Weekend or Public Holiday
        Error = 6      // E.g., Punched IN but never punched OUT
    }

    public enum LeaveRequestStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Cancelled = 4
    }
}
