using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Queries
{
    public class AdjustmentGridDto
    {
        public string ReferenceNo { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Warehouse { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public decimal TotalImpactValue { get; set; }
        public int ItemsAffected { get; set; }
    }

    public class GetAdjustmentsQuery : IRequest<Result<List<AdjustmentGridDto>>> { }

    public class GetAdjustmentsHandler : IRequestHandler<GetAdjustmentsQuery, Result<List<AdjustmentGridDto>>>
    {
        private readonly IErpDbContext _context;
        public GetAdjustmentsHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<AdjustmentGridDto>>> Handle(GetAdjustmentsQuery request, CancellationToken cancellationToken)
        {
            // Fetch transactions related to adjustments. Assuming you map Shrinkage to TransferOut/ProductionOut 
            // and Surplus to Receipt/PurchaseIn internally, or you added AdjustmentIn/Out to StockTransactionType.
            // We identify adjustments by the ReferenceDocNo prefix "ADJ-" or "StockAdjustment".

            var txns = await _context.StockTransactions
                .Include(t => t.Warehouse)
                .Where(t => t.ReferenceDocNo.StartsWith("ADJ-") || t.Notes.Contains("Shrinkage") || t.Notes.Contains("Surplus"))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var grid = txns
                .GroupBy(t => new { t.ReferenceDocNo, t.Date, WhName = t.Warehouse.Name, t.Notes })
                .Select(g => new AdjustmentGridDto
                {
                    ReferenceNo = g.Key.ReferenceDocNo,
                    Date = g.Key.Date,
                    Warehouse = g.Key.WhName,
                    Notes = g.Key.Notes.Split(':')[1].Trim(), // Strip "Shrinkage: " prefix for cleaner display
                    TotalImpactValue = g.Sum(x => x.TotalValue),
                    ItemsAffected = g.Select(x => x.ProductVariantId).Distinct().Count()
                })
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.ReferenceNo)
                .ToList();

            return Result<List<AdjustmentGridDto>>.Success(grid);
        }
    }
}
