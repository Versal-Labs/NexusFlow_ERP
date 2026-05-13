using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Payroll.Queries
{
    public class GetEpfReturnFileQuery : IRequest<Result<byte[]>>
    {
        public int PayrollPeriodId { get; set; }
    }

    public class GetEpfReturnFileHandler : IRequestHandler<GetEpfReturnFileQuery, Result<byte[]>>
    {
        private readonly IErpDbContext _context;
        public GetEpfReturnFileHandler(IErpDbContext context) => _context = context;

        public async Task<Result<byte[]>> Handle(GetEpfReturnFileQuery request, CancellationToken cancellationToken)
        {
            var slips = await _context.PayrollSlips
                .Include(s => s.Employee)
                .Include(s => s.LineItems)
                .Where(s => s.PayrollPeriodId == request.PayrollPeriodId)
                .ToListAsync(cancellationToken);

            var csvBuilder = new StringBuilder();
            // Sri Lanka EPF C-Form Standard Columns
            csvBuilder.AppendLine("EPF_No,NIC,EmployeeName,TotalEarnings,EmployeeContrib_8,EmployerContrib_12,TotalEPF_20");

            foreach (var slip in slips)
            {
                decimal empEpf8 = slip.LineItems.Where(l => l.Description.Contains("EPF") && l.Type == 2).Sum(l => l.Amount);
                decimal totalEpf = empEpf8 + slip.EmployerEPF;

                // Reverse-calculate the EPF Base Earnings (if 8% is X, Base is X / 0.08)
                decimal epfBaseEarnings = empEpf8 > 0 ? (empEpf8 / 0.08m) : 0;

                var empName = $"{slip.Employee.FirstName} {slip.Employee.LastName}".Replace(",", "");

                csvBuilder.AppendLine($"{slip.Employee.EPF_No},{slip.Employee.NIC},{empName},{epfBaseEarnings:F2},{empEpf8:F2},{slip.EmployerEPF:F2},{totalEpf:F2}");
            }

            var fileBytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            return Result<byte[]>.Success(fileBytes);
        }
    }
}
