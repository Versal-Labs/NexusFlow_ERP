using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.AppCore.Services
{
    public sealed class FinancialPeriodService : IFinancialPeriodService
    {
        private readonly IErpDbContext _context;

        public FinancialPeriodService(IErpDbContext context) => _context = context;

        public async Task<FinancialPeriodStatusDto> GetStatusAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var businessDate = date.Date;
            var period = await _context.FinancialPeriods
                .AsNoTracking()
                .Where(x => businessDate >= x.StartDate.Date && businessDate <= x.EndDate.Date)
                .OrderBy(x => x.StartDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (period == null)
            {
                return new FinancialPeriodStatusDto
                {
                    BusinessDate = businessDate,
                    Status = "Missing",
                    Message = $"No financial period covers {businessDate:yyyy-MM-dd}. Create or open a period before entering transactions."
                };
            }

            var status = period.IsClosed ? "Closed" : "Open";
            return new FinancialPeriodStatusDto
            {
                BusinessDate = businessDate,
                Status = status,
                PeriodId = period.Id,
                Year = period.Year,
                Month = period.Month,
                StartDate = period.StartDate.Date,
                EndDate = period.EndDate.Date,
                Message = period.IsClosed
                    ? $"Financial period {period.Year}-{period.Month:00} is closed for {businessDate:yyyy-MM-dd}."
                    : $"Financial period {period.Year}-{period.Month:00} is open."
            };
        }
    }
}
