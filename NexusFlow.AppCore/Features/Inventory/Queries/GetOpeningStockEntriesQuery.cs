using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Inventory.Queries
{
    public class OpeningStockEntryDto
    {
        public string ReferenceNo { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Warehouse { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int ItemsAffected { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class GetOpeningStockEntriesQuery : IRequest<Result<List<OpeningStockEntryDto>>> { }

    public class GetOpeningStockEntriesHandler : IRequestHandler<GetOpeningStockEntriesQuery, Result<List<OpeningStockEntryDto>>>
    {
        private readonly IErpDbContext _context;

        public GetOpeningStockEntriesHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<OpeningStockEntryDto>>> Handle(GetOpeningStockEntriesQuery request, CancellationToken cancellationToken)
        {
            var transactions = await _context.StockTransactions
                .Include(t => t.Warehouse)
                .Where(t => t.Type == StockTransactionType.OpeningBalance)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var entries = transactions
                .GroupBy(t => new { t.ReferenceDocNo, Warehouse = t.Warehouse.Name })
                .Select(g => new OpeningStockEntryDto
                {
                    ReferenceNo = g.Key.ReferenceDocNo,
                    Date = g.Min(t => t.Date),
                    Warehouse = g.Key.Warehouse,
                    Notes = g.Select(t => t.Notes).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? string.Empty,
                    ItemsAffected = g.Select(t => t.ProductVariantId).Distinct().Count(),
                    TotalValue = g.Sum(t => t.TotalValue)
                })
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.ReferenceNo)
                .ToList();

            return Result<List<OpeningStockEntryDto>>.Success(entries);
        }
    }
}
