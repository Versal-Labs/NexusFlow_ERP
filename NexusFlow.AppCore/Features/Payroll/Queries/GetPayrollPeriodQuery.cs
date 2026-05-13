using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Payroll.Queries
{
    // 1. The DTOs
    public class PayrollSlipGridDto
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public decimal GrossBasic { get; set; }
        public decimal TotalAllowances { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetPay { get; set; }
    }

    public class PayrollPeriodDto
    {
        public int Id { get; set; }
        public string MonthYear { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Draft, Posted, Paid
        public List<PayrollSlipGridDto> Slips { get; set; } = new();
    }

    // 2. The Query
    public class GetPayrollPeriodQuery : IRequest<Result<PayrollPeriodDto>>
    {
        public string MonthYear { get; set; } = string.Empty;
    }

    // 3. The Handler
    public class GetPayrollPeriodHandler : IRequestHandler<GetPayrollPeriodQuery, Result<PayrollPeriodDto>>
    {
        private readonly IErpDbContext _context;
        public GetPayrollPeriodHandler(IErpDbContext context) => _context = context;

        public async Task<Result<PayrollPeriodDto>> Handle(GetPayrollPeriodQuery request, CancellationToken cancellationToken)
        {
            var period = await _context.PayrollPeriods
                .Include(p => p.Slips)
                    .ThenInclude(s => s.Employee)
                .FirstOrDefaultAsync(p => p.MonthYear == request.MonthYear, cancellationToken);

            // It is completely normal for a period to be null (e.g., they haven't generated the draft yet).
            // We return a Success with a null object, NOT a failure. The JS expects this!
            if (period == null)
                return Result<PayrollPeriodDto>.Success(null, "No payroll generated for this month yet.");

            var dto = new PayrollPeriodDto
            {
                Id = period.Id,
                MonthYear = period.MonthYear,
                Status = period.Status.ToString(),
                Slips = period.Slips.Select(s => new PayrollSlipGridDto
                {
                    Id = s.Id,
                    EmployeeName = $"{s.Employee.FirstName} {s.Employee.LastName}",
                    EmployeeCode = s.Employee.EmployeeCode,
                    GrossBasic = s.GrossBasic,
                    TotalAllowances = s.TotalAllowances,
                    TotalDeductions = s.TotalDeductions,
                    NetPay = s.NetPay
                }).ToList()
            };

            return Result<PayrollPeriodDto>.Success(dto);
        }
    }
}
