using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.HR.Queries
{
    public class AttendanceGridDto
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string ShiftName { get; set; } = "No Shift Assigned";
        public string? FirstIn { get; set; }
        public string? LastOut { get; set; }
        public int LateMinutes { get; set; }
        public int OvertimeMinutes { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public class GetDailyAttendanceQuery : IRequest<Result<List<AttendanceGridDto>>>
    {
        public DateTime Date { get; set; }
    }

    public class GetDailyAttendanceHandler : IRequestHandler<GetDailyAttendanceQuery, Result<List<AttendanceGridDto>>>
    {
        private readonly IErpDbContext _context;
        public GetDailyAttendanceHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<AttendanceGridDto>>> Handle(GetDailyAttendanceQuery request, CancellationToken cancellationToken)
        {
            var records = await _context.DailyAttendanceRecords
                .Include(r => r.Employee)
                    .ThenInclude(e => e.ShiftProfile)
                .Where(r => r.Date == request.Date)
                .OrderBy(r => r.Employee.FirstName)
                .ToListAsync(cancellationToken);

            var dtos = records.Select(r => new AttendanceGridDto
            {
                Id = r.Id,
                EmployeeName = $"{r.Employee.FirstName} {r.Employee.LastName}",
                EmployeeCode = r.Employee.EmployeeCode,
                ShiftName = r.Employee.ShiftProfile?.Name ?? "No Shift Assigned",
                FirstIn = r.FirstIn?.ToString("HH:mm"),
                LastOut = r.LastOut?.ToString("HH:mm"),
                LateMinutes = r.LateMinutes,
                OvertimeMinutes = r.OvertimeMinutes,
                Status = r.Status.ToString(),
                Remarks = r.Remarks
            }).ToList();

            return Result<List<AttendanceGridDto>>.Success(dtos);
        }
    }
}
