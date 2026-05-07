using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.HR;
using NexusFlow.Shared.Wrapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.HR.Commands
{
    public class BiometricPunchDto
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public DateTime PunchTime { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    public class SyncBiometricDataCommand : IRequest<Result<int>>
    {
        public List<BiometricPunchDto> Punches { get; set; } = new();
    }

    public class SyncBiometricDataHandler : IRequestHandler<SyncBiometricDataCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public SyncBiometricDataHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(SyncBiometricDataCommand request, CancellationToken cancellationToken)
        {
            if (!request.Punches.Any()) return Result<int>.Failure("No punches received.");

            // 1. Get a map of EmployeeCode to EmployeeId (To avoid hitting the DB in a loop)
            var empCodes = request.Punches.Select(p => p.EmployeeCode).Distinct().ToList();
            var employeeMap = await _context.Employees
                .Where(e => empCodes.Contains(e.EmployeeCode))
                .ToDictionaryAsync(e => e.EmployeeCode, e => e.Id, cancellationToken);

            var logsToInsert = new List<AttendanceLog>();

            foreach (var punch in request.Punches)
            {
                // Only insert if we recognize the employee code
                if (employeeMap.TryGetValue(punch.EmployeeCode, out int empId))
                {
                    logsToInsert.Add(new AttendanceLog
                    {
                        EmployeeId = empId,
                        PunchTime = punch.PunchTime,
                        DeviceId = punch.DeviceId
                    });
                }
            }

            if (logsToInsert.Any())
            {
                await _context.AttendanceLogs.AddRangeAsync(logsToInsert, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result<int>.Success(logsToInsert.Count, $"Successfully synced {logsToInsert.Count} punches.");
        }
    }
}
