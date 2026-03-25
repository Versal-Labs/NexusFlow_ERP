using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.HR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.HR.Queries
{
    public class GetEmployeesQuery : IRequest<Result<List<EmployeeDto>>> { }

    public class GetEmployeesHandler : IRequestHandler<GetEmployeesQuery, Result<List<EmployeeDto>>>
    {
        private readonly IErpDbContext _context;

        public GetEmployeesHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<EmployeeDto>>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.Employees
                .AsNoTracking()
                .OrderBy(e => e.FirstName)
                .Select(e => new EmployeeDto
                {
                    Id = e.Id,
                    EmployeeCode = e.EmployeeCode,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Email = e.Email,
                    Phone = e.Phone,
                    NIC = e.NIC,
                    BasicSalary = e.BasicSalary,
                    EPF_No = e.EPF_No,
                    BankName = e.BankName,
                    BankAccountNo = e.BankAccountNo,
                    IsSalesRep = e.IsSalesRep
                })
                .ToListAsync(cancellationToken);

            return Result<List<EmployeeDto>>.Success(data);
        }
    }
}
