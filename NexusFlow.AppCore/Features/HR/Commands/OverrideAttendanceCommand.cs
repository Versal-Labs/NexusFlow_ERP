using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.HR.Commands
{
    public class OverrideAttendanceCommand : IRequest<Result<int>>
    {
        public int RecordId { get; set; }
        public TimeSpan? FirstInTime { get; set; }
        public TimeSpan? LastOutTime { get; set; }
        public AttendanceStatus Status { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }

    public class OverrideAttendanceHandler : IRequestHandler<OverrideAttendanceCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public OverrideAttendanceHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(OverrideAttendanceCommand request, CancellationToken cancellationToken)
        {
            var record = await _context.DailyAttendanceRecords
                .Include(r => r.Employee)
                    .ThenInclude(e => e.ShiftProfile)
                .FirstOrDefaultAsync(r => r.Id == request.RecordId, cancellationToken);

            if (record == null) return Result<int>.Failure("Record not found.");

            // Apply new times (Combine existing Date with new TimeSpan)
            if (request.FirstInTime.HasValue)
                record.FirstIn = record.Date.Date.Add(request.FirstInTime.Value);
            else
                record.FirstIn = null;

            if (request.LastOutTime.HasValue)
                record.LastOut = record.Date.Date.Add(request.LastOutTime.Value);
            else
                record.LastOut = null;

            record.Status = request.Status;
            record.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? "Manual Override" : request.Remarks;

            // Recalculate Late and Overtime if they are Present/HalfDay and have a shift profile
            record.LateMinutes = 0;
            record.OvertimeMinutes = 0;

            if ((record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.HalfDay)
                && record.Employee.ShiftProfile != null
                && record.FirstIn.HasValue
                && record.LastOut.HasValue)
            {
                var shift = record.Employee.ShiftProfile;

                // Check Late
                if (record.FirstIn.Value.TimeOfDay > shift.StartTime.Add(TimeSpan.FromMinutes(shift.GracePeriodMinutes)))
                    record.LateMinutes = (int)(record.FirstIn.Value.TimeOfDay - shift.StartTime).TotalMinutes;

                // Check Overtime
                if (record.LastOut.Value.TimeOfDay > shift.EndTime)
                    record.OvertimeMinutes = (int)(record.LastOut.Value.TimeOfDay - shift.EndTime).TotalMinutes;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(record.Id, "Attendance record overridden successfully.");
        }
    }
}
