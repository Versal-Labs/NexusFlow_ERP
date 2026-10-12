using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Reporting.Queries
{
    public class CustomerStatementRowDto
    {
        public string Date { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; } // Increases balance (Invoice)
        public decimal Credit { get; set; } // Decreases balance (Payment, Return)
        public decimal Balance { get; set; } // Running Balance
    }

    public class GetCustomerStatementQuery : IRequest<Result<List<CustomerStatementRowDto>>>
    {
        public int CustomerId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class GetCustomerStatementHandler : IRequestHandler<GetCustomerStatementQuery, Result<List<CustomerStatementRowDto>>>
    {
        private readonly IErpDbContext _context;
        public GetCustomerStatementHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<CustomerStatementRowDto>>> Handle(GetCustomerStatementQuery request, CancellationToken cancellationToken)
        {
            if (request.CustomerId <= 0) return Result<List<CustomerStatementRowDto>>.Failure("Customer ID is required.");

            if (request.StartDate.Date > request.EndDate.Date) return Result<List<CustomerStatementRowDto>>.Failure("Start date must precede end date.");
            if (!await _context.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken)) return Result<List<CustomerStatementRowDto>>.Failure("Customer not found.");
            var snapshot = await new NexusFlow.AppCore.Services.CustomerBalanceService(_context).GetAsync(request.CustomerId, request.EndDate, cancellationToken);
            decimal runningBalance = snapshot.Events.Where(x => x.Date < request.StartDate.Date).Sum(x => x.Amount);
            var statement = new List<CustomerStatementRowDto> { new() {
                Date = request.StartDate.ToString("yyyy-MM-dd"), TransactionType = "Opening Balance", ReferenceNo = "-",
                Description = "Balance Brought Forward", Balance = runningBalance
            }};
            foreach (var item in snapshot.Events.Where(x => x.Date >= request.StartDate.Date).OrderBy(x => x.Date).ThenBy(x => x.Reference).ThenBy(x => x.Type))
            {
                runningBalance += item.Amount;
                statement.Add(new() { Date = item.Date.ToString("yyyy-MM-dd"), TransactionType = item.Type,
                    ReferenceNo = item.Reference, Description = item.Description, Debit = Math.Max(item.Amount, 0),
                    Credit = Math.Max(-item.Amount, 0), Balance = runningBalance });
            }

            return Result<List<CustomerStatementRowDto>>.Success(statement);
        }
    }
}
