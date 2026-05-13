using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Payroll;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Payroll.Queries
{
    public class GetBankTransferFileQuery : IRequest<Result<byte[]>>
    {
        public int PayrollPeriodId { get; set; }
    }

    public class GetBankTransferFileHandler : IRequestHandler<GetBankTransferFileQuery, Result<byte[]>>
    {
        private readonly IErpDbContext _context;
        public GetBankTransferFileHandler(IErpDbContext context) => _context = context;

        public async Task<Result<byte[]>> Handle(GetBankTransferFileQuery request, CancellationToken cancellationToken)
        {
            var period = await _context.PayrollPeriods.FindAsync(new object[] { request.PayrollPeriodId }, cancellationToken);
            if (period == null || period.Status < PayrollPeriodStatus.Posted)
                return Result<byte[]>.Failure("Payroll must be Posted before generating bank files.");

            var slips = await _context.PayrollSlips
                .Include(s => s.Employee)
                .Where(s => s.PayrollPeriodId == request.PayrollPeriodId && s.NetPay > 0)
                .ToListAsync(cancellationToken);

            var csvBuilder = new StringBuilder();
            // Standard Bank Header (Adjust according to your specific bank's format)
            csvBuilder.AppendLine("BankName,AccountNo,EmployeeName,Amount,Reference");

            foreach (var slip in slips)
            {
                // Clean inputs to prevent CSV injection/breaking
                var empName = $"{slip.Employee.FirstName} {slip.Employee.LastName}".Replace(",", " ");
                var refStr = $"SALARY {period.MonthYear}";

                csvBuilder.AppendLine($"{slip.Employee.BankName},{slip.Employee.BankAccountNo},{empName},{slip.NetPay:F2},{refStr}");
            }

            // Also, update the status to Paid since we are now generating the disbursement file!
            if (period.Status == PayrollPeriodStatus.Posted)
            {
                period.Status = PayrollPeriodStatus.Paid;
                await _context.SaveChangesAsync(cancellationToken);
            }

            var fileBytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            return Result<byte[]>.Success(fileBytes);
        }
    }
}
