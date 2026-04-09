using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Queries
{
    public class TransferGridDto
    {
        public string ReferenceNo { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string SourceWarehouse { get; set; } = string.Empty;
        public string TargetWarehouse { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class GetTransfersQuery : IRequest<Result<List<TransferGridDto>>> { }

    public class GetTransfersHandler : IRequestHandler<GetTransfersQuery, Result<List<TransferGridDto>>>
    {
        private readonly IErpDbContext _context;
        public GetTransfersHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<TransferGridDto>>> Handle(GetTransfersQuery request, CancellationToken cancellationToken)
        {
            // Fetch TransferOut transactions to build the header grid
            var outTxns = await _context.StockTransactions
                .Include(t => t.Warehouse)
                .Where(t => t.Type == StockTransactionType.TransferOut)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Fetch TransferIn transactions just to easily get the Target Warehouse names
            var inTxns = await _context.StockTransactions
                .Include(t => t.Warehouse)
                .Where(t => t.Type == StockTransactionType.TransferIn)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var grid = outTxns
                .GroupBy(t => new { t.ReferenceDocNo, t.Date, SourceWh = t.Warehouse.Name })
                .Select(g => new TransferGridDto
                {
                    ReferenceNo = g.Key.ReferenceDocNo,
                    Date = g.Key.Date,
                    SourceWarehouse = g.Key.SourceWh,
                    // Find the matching 'IN' transaction for this ref number to get the target WH
                    TargetWarehouse = inTxns.FirstOrDefault(i => i.ReferenceDocNo == g.Key.ReferenceDocNo)?.Warehouse.Name ?? "Unknown",
                    TotalItems = g.Count(),
                    TotalValue = g.Sum(x => x.TotalValue)
                })
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.ReferenceNo)
                .ToList();

            return Result<List<TransferGridDto>>.Success(grid);
        }
    }
}
