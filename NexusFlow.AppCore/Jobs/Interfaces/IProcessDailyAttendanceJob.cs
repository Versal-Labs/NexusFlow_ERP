using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Jobs.Interfaces
{
    public interface IProcessDailyAttendanceJob
    {
        Task ExecuteAsync(DateTime? targetDate = null, CancellationToken cancellationToken = default);
    }
}
