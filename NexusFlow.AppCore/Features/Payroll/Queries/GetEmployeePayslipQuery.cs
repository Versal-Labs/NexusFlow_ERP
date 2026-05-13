using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Payroll.Queries
{
    public class PayslipDto
    {
        public string CompanyName { get; set; } = "NexusFlow Enterprise";
        public string MonthYear { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string NIC { get; set; } = string.Empty;
        public string EPFNo { get; set; } = string.Empty;

        public decimal BasicSalary { get; set; }
        public List<KeyValuePair<string, decimal>> Allowances { get; set; } = new();
        public List<KeyValuePair<string, decimal>> Deductions { get; set; } = new();

        public decimal GrossPay { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetPay { get; set; }
    }

    public class GetEmployeePayslipQuery : IRequest<Result<PayslipDto>>
    {
        public int PayrollSlipId { get; set; }
    }

    public class GetEmployeePayslipHandler : IRequestHandler<GetEmployeePayslipQuery, Result<PayslipDto>>
    {
        private readonly IErpDbContext _context;
        public GetEmployeePayslipHandler(IErpDbContext context) => _context = context;

        public async Task<Result<PayslipDto>> Handle(GetEmployeePayslipQuery request, CancellationToken cancellationToken)
        {
            var slip = await _context.PayrollSlips
                .Include(s => s.Employee)
                .Include(s => s.PayrollPeriod)
                .Include(s => s.LineItems)
                .FirstOrDefaultAsync(s => s.Id == request.PayrollSlipId, cancellationToken);

            if (slip == null) return Result<PayslipDto>.Failure("Payslip not found.");

            var dto = new PayslipDto
            {
                MonthYear = slip.PayrollPeriod.MonthYear,
                EmployeeName = $"{slip.Employee.FirstName} {slip.Employee.LastName}",
                EmployeeCode = slip.Employee.EmployeeCode,
                NIC = slip.Employee.NIC,
                EPFNo = slip.Employee.EPF_No,
                BasicSalary = slip.GrossBasic,
                GrossPay = slip.GrossBasic + slip.TotalAllowances,
                TotalDeductions = slip.TotalDeductions,
                NetPay = slip.NetPay
            };

            foreach (var item in slip.LineItems.Where(l => l.Description != "Basic Salary"))
            {
                if (item.Type == 1) // Allowance
                    dto.Allowances.Add(new KeyValuePair<string, decimal>(item.Description, item.Amount));
                else // Deduction
                    dto.Deductions.Add(new KeyValuePair<string, decimal>(item.Description, item.Amount));
            }

            return Result<PayslipDto>.Success(dto);
        }
    }
}
