using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Jobs.Interfaces;
using NexusFlow.Domain.Entities.HR;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Jobs
{
    public class ProcessDailyAttendanceJob : IProcessDailyAttendanceJob
    {
        private readonly IErpDbContext _context;
        private readonly ILogger<ProcessDailyAttendanceJob> _logger;

        public ProcessDailyAttendanceJob(IErpDbContext context, ILogger<ProcessDailyAttendanceJob> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Target date defaults to null, meaning it will process "Yesterday"
        public async Task ExecuteAsync(DateTime? targetDate = null, CancellationToken cancellationToken = default)
        {
            var dateToProcess = (targetDate ?? DateTime.UtcNow.AddDays(-1)).Date;
            _logger.LogInformation($"Starting Daily Attendance Processing for {dateToProcess:yyyy-MM-dd}");

            var startOfDay = dateToProcess;
            var endOfDay = dateToProcess.AddDays(1).AddTicks(-1);

            // 1. Fetch all active employees with their Shift Profiles
            var employees = await _context.Employees
                .Include(e => e.ShiftProfile)
                // Assuming you have an IsActive flag. If not, omit this condition.
                // .Where(e => e.IsActive) 
                .ToListAsync(cancellationToken);

            // 2. Fetch all raw punches for the target date
            var rawPunches = await _context.AttendanceLogs
                .Where(l => l.PunchTime >= startOfDay && l.PunchTime <= endOfDay)
                .ToListAsync(cancellationToken);

            // 3. Fetch all approved leaves overlapping this date
            var approvedLeaves = await _context.LeaveRequests
                .Where(l => l.Status == LeaveRequestStatus.Approved && l.StartDate <= dateToProcess && l.EndDate >= dateToProcess)
                .ToListAsync(cancellationToken);

            // 4. Clean up any existing processed records for this date (Idempotency)
            var existingRecords = await _context.DailyAttendanceRecords
                .Where(r => r.Date == dateToProcess)
                .ToListAsync(cancellationToken);

            if (existingRecords.Any())
            {
                _context.DailyAttendanceRecords.RemoveRange(existingRecords);
            }

            var newRecords = new List<DailyAttendanceRecord>();

            // 5. THE ENGINE: Process each employee
            foreach (var emp in employees)
            {
                var record = new DailyAttendanceRecord
                {
                    EmployeeId = emp.Id,
                    Date = dateToProcess
                };

                // Find punches for this specific employee
                var empPunches = rawPunches.Where(p => p.EmployeeId == emp.Id).OrderBy(p => p.PunchTime).ToList();
                var isOnLeave = approvedLeaves.Any(l => l.EmployeeId == emp.Id);

                // Check if it's a weekend (Assuming Saturday/Sunday are rest days - make this configurable later if needed)
                bool isWeekend = dateToProcess.DayOfWeek == DayOfWeek.Saturday || dateToProcess.DayOfWeek == DayOfWeek.Sunday;

                if (empPunches.Any())
                {
                    // Employee showed up!
                    record.FirstIn = empPunches.First().PunchTime;
                    record.LastOut = empPunches.Last().PunchTime; // If only 1 punch, FirstIn == LastOut

                    record.Status = AttendanceStatus.Present;

                    // Calculate Late Minutes and Overtime using the Shift Profile
                    if (emp.ShiftProfile != null && record.FirstIn.HasValue && record.LastOut.HasValue)
                    {
                        var firstInTime = record.FirstIn.Value.TimeOfDay;
                        var lastOutTime = record.LastOut.Value.TimeOfDay;

                        // Check Late
                        var expectedStartTime = emp.ShiftProfile.StartTime;
                        var gracePeriod = TimeSpan.FromMinutes(emp.ShiftProfile.GracePeriodMinutes);

                        if (firstInTime > expectedStartTime.Add(gracePeriod))
                        {
                            record.LateMinutes = (int)(firstInTime - expectedStartTime).TotalMinutes;
                        }

                        // Check Overtime
                        var expectedEndTime = emp.ShiftProfile.EndTime;
                        if (lastOutTime > expectedEndTime)
                        {
                            record.OvertimeMinutes = (int)(lastOutTime - expectedEndTime).TotalMinutes;
                        }

                        // Check Half Day
                        var minutesWorked = (record.LastOut.Value - record.FirstIn.Value).TotalMinutes;
                        if (minutesWorked > 0 && minutesWorked < emp.ShiftProfile.HalfDayThresholdMinutes)
                        {
                            record.Status = AttendanceStatus.HalfDay;
                            record.Remarks = "Worked less than half-day threshold.";
                        }
                    }

                    if (record.FirstIn == record.LastOut)
                    {
                        record.Status = AttendanceStatus.Error;
                        record.Remarks = "Missing OUT punch.";
                    }
                }
                else
                {
                    // Employee did not show up. Why?
                    if (isOnLeave)
                    {
                        record.Status = AttendanceStatus.OnLeave;
                        record.Remarks = "Approved Leave";
                    }
                    else if (isWeekend)
                    {
                        record.Status = AttendanceStatus.RestDay;
                    }
                    else
                    {
                        record.Status = AttendanceStatus.Absent;
                        record.Remarks = "No punches recorded.";
                    }
                }

                newRecords.Add(record);
            }

            // 6. Save to database
            await _context.DailyAttendanceRecords.AddRangeAsync(newRecords, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Successfully processed attendance for {newRecords.Count} employees.");
        }
    }
}
