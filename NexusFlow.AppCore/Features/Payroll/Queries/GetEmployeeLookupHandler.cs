using MediatR;
using NexusFlow.AppCore.Interfaces;
using Microsoft.EntityFrameworkCore;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Payroll.Queries
{
    public class GetEmployeeLookupQuery : IRequest<Result<List<object>>> { }

    public class GetEmployeeLookupHandler : IRequestHandler<GetEmployeeLookupQuery, Result<List<object>>>
    {
        private readonly IErpDbContext _context;
        public GetEmployeeLookupHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<object>>> Handle(GetEmployeeLookupQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.Employees
                .Select(e => (object)new // Explicit cast
                {
                    id = e.Id,
                    firstName = e.FirstName,
                    lastName = e.LastName,
                    employeeCode = e.EmployeeCode
                }).ToListAsync(cancellationToken);

            return Result<List<object>>.Success(data);
        }
    }
}
