using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Payroll.Queries
{
    // ==========================================
    // 1. COMPONENTS QUERY
    // ==========================================
    public class GetPayrollComponentsQuery : IRequest<Result<List<object>>> { }

    public class GetPayrollComponentsHandler : IRequestHandler<GetPayrollComponentsQuery, Result<List<object>>>
    {
        private readonly IErpDbContext _context;
        public GetPayrollComponentsHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<object>>> Handle(GetPayrollComponentsQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.PayrollComponents
                .Select(c => (object)new // Explicitly cast to object here
                {
                    id = c.Id,
                    name = c.Name,
                    type = (int)c.Type,
                    calculationType = (int)c.CalculationType,
                    defaultRate = c.DefaultRate,
                    isTaxable = c.IsTaxable,
                    isEPFCalculable = c.IsEPFCalculable
                }).ToListAsync(cancellationToken);

            return Result<List<object>>.Success(data); // Pass data directly
        }
    }

    // ==========================================
    // 2. ASSIGNMENTS QUERY
    // ==========================================
    public class GetEmployeeAssignmentsQuery : IRequest<Result<List<object>>> { }

    public class GetEmployeeAssignmentsHandler : IRequestHandler<GetEmployeeAssignmentsQuery, Result<List<object>>>
    {
        private readonly IErpDbContext _context;
        public GetEmployeeAssignmentsHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<object>>> Handle(GetEmployeeAssignmentsQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.EmployeePayrollComponents
                .Include(a => a.Employee)
                .Include(a => a.PayrollComponent)
                .Select(a => (object)new // Explicitly cast to object here
                {
                    id = a.Id,
                    employeeName = $"{a.Employee.FirstName} {a.Employee.LastName} ({a.Employee.EmployeeCode})",
                    componentName = a.PayrollComponent.Name,
                    overrideRate = a.OverrideRate,
                    isActive = a.IsActive
                }).ToListAsync(cancellationToken);

            return Result<List<object>>.Success(data);
        }
    }

    // ==========================================
    // 3. LOANS QUERY
    // ==========================================
    public class GetActiveLoansQuery : IRequest<Result<List<object>>> { }

    public class GetActiveLoansHandler : IRequestHandler<GetActiveLoansQuery, Result<List<object>>>
    {
        private readonly IErpDbContext _context;
        public GetActiveLoansHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<object>>> Handle(GetActiveLoansQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.EmployeeLoans
                .Include(l => l.Employee)
                .Select(l => (object)new // Explicitly cast to object here
                {
                    id = l.Id,
                    employeeName = $"{l.Employee.FirstName} {l.Employee.LastName}",
                    disbursementDate = l.DisbursementDate,
                    principalAmount = l.PrincipalAmount,
                    termInMonths = l.TermInMonths,
                    emiAmount = l.EMIAmount,
                    status = (int)l.Status
                }).ToListAsync(cancellationToken);

            return Result<List<object>>.Success(data);
        }
    }
}
