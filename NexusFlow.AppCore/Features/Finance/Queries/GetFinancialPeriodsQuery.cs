using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Queries
{
    public class GetFinancialPeriodsQuery : IRequest<Result<List<FinancialPeriodDto>>> { }

    public class FinancialPeriodDto
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public bool IsClosed { get; set; }
    }

    public class GetFinancialPeriodsHandler : IRequestHandler<GetFinancialPeriodsQuery, Result<List<FinancialPeriodDto>>>
    {
        private readonly IErpDbContext _context;

        public GetFinancialPeriodsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<FinancialPeriodDto>>> Handle(GetFinancialPeriodsQuery request, CancellationToken cancellationToken)
        {
            var periods = await _context.FinancialPeriods
                .AsNoTracking()
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .Select(p => new FinancialPeriodDto
                {
                    Id = p.Id,
                    Year = p.Year,
                    Month = p.Month,
                    StartDate = p.StartDate.ToString("yyyy-MM-dd"),
                    EndDate = p.EndDate.ToString("yyyy-MM-dd"),
                    IsClosed = p.IsClosed
                })
                .ToListAsync(cancellationToken);

            return Result<List<FinancialPeriodDto>>.Success(periods);
        }
    }
}
