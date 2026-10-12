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
    public class SupplierStatementRowDto
    {
        public string Date { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Credit { get; set; } // Increases what we owe (Bill)
        public decimal Debit { get; set; } // Decreases what we owe (Payment to supplier)
        public decimal Balance { get; set; } // Running Balance
    }

    public class GetSupplierStatementQuery : IRequest<Result<List<SupplierStatementRowDto>>>
    {
        public int SupplierId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class GetSupplierStatementHandler : IRequestHandler<GetSupplierStatementQuery, Result<List<SupplierStatementRowDto>>>
    {
        private readonly IErpDbContext _context;
        public GetSupplierStatementHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<SupplierStatementRowDto>>> Handle(GetSupplierStatementQuery request, CancellationToken cancellationToken)
        {
            if (request.SupplierId <= 0) return Result<List<SupplierStatementRowDto>>.Failure("Supplier ID is required.");

            if (request.StartDate.Date > request.EndDate.Date) return Result<List<SupplierStatementRowDto>>.Failure("Start date must not follow end date.");
            var snapshot = await new NexusFlow.AppCore.Services.SupplierBalanceService(_context).GetAsync(request.SupplierId, request.EndDate, cancellationToken);
            var runningBalance = snapshot.Events.Where(x => x.Date.Date < request.StartDate.Date).Sum(x => x.Amount);
            var statement = new List<SupplierStatementRowDto> { new() { Date = request.StartDate.ToString("yyyy-MM-dd"), TransactionType = "Opening Balance", ReferenceNo = "-", Description = "Balance brought forward", Balance = runningBalance } };
            foreach (var e in snapshot.Events.Where(x => x.Date.Date >= request.StartDate.Date).OrderBy(x => x.Date).ThenBy(x => x.Reference))
            {
                runningBalance += e.Amount;
                statement.Add(new SupplierStatementRowDto { Date = e.Date.ToString("yyyy-MM-dd"), TransactionType = e.Type, ReferenceNo = e.Reference, Description = e.Description, Credit = Math.Max(0, e.Amount), Debit = Math.Max(0, -e.Amount), Balance = runningBalance });
            }

            return Result<List<SupplierStatementRowDto>>.Success(statement);
        }
    }
}
