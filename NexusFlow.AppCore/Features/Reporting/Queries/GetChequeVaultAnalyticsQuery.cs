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
    public class ChequeVaultAnalyticsDto
    {
        public string ChequeNumber { get; set; } = string.Empty;
        public string PDCDate { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Bank { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;

        // Treasury helper: Negative means overdue/matured, Positive means days until it can be deposited
        public int DaysToMaturity { get; set; }
    }

    public class GetChequeVaultAnalyticsQuery : IRequest<Result<List<ChequeVaultAnalyticsDto>>>
    {
        public DateTime? StartDate { get; set; } // Based on PDC Date
        public DateTime? EndDate { get; set; }
        public int? CustomerId { get; set; }
        public int? BankId { get; set; }
        public ChequeStatus? Status { get; set; }
    }

    public class GetChequeVaultAnalyticsHandler : IRequestHandler<GetChequeVaultAnalyticsQuery, Result<List<ChequeVaultAnalyticsDto>>>
    {
        private readonly IErpDbContext _context;
        public GetChequeVaultAnalyticsHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<ChequeVaultAnalyticsDto>>> Handle(GetChequeVaultAnalyticsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.ChequeRegisters
                .Include(c => c.Customer)
                .Include(c => c.BankBranch)
                    .ThenInclude(b => b.Bank)
                .AsQueryable();

            if (request.StartDate.HasValue) query = query.Where(c => c.ChequeDate >= request.StartDate.Value.Date);
            if (request.EndDate.HasValue) query = query.Where(c => c.ChequeDate <= request.EndDate.Value.Date.AddDays(1).AddTicks(-1));
            if (request.CustomerId.HasValue) query = query.Where(c => c.CustomerId == request.CustomerId.Value);
            if (request.Status.HasValue) query = query.Where(c => c.Status == request.Status.Value);

            // Note: BankId filtering goes through the BankBranch relationship
            if (request.BankId.HasValue) query = query.Where(c => c.BankBranch.BankId == request.BankId.Value);

            var today = DateTime.UtcNow.Date;

            var data = await query
                .OrderBy(c => c.ChequeDate) // Ascending so oldest/maturing PDCs show first
                .Select(c => new ChequeVaultAnalyticsDto
                {
                    ChequeNumber = c.ChequeNumber,
                    PDCDate = c.ChequeDate.ToString("yyyy-MM-dd"),
                    Customer = c.Customer.Name,
                    Bank = c.BankBranch.Bank.Name,
                    Branch = c.BankBranch.BranchName,
                    Amount = c.Amount,
                    Status = c.Status.ToString(),
                    DaysToMaturity = (c.ChequeDate.Date - today).Days
                })
                .ToListAsync(cancellationToken);

            return Result<List<ChequeVaultAnalyticsDto>>.Success(data);
        }
    }
}
