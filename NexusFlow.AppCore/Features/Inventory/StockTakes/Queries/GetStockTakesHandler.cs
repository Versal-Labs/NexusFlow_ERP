using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.StockTakes.Queries
{
    public class StockTakeDto
    {
        public int Id { get; set; }
        public string StockTakeNumber { get; set; } = string.Empty;
        public string Date { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public decimal TotalVarianceValue { get; set; }
    }

    public class GetStockTakesQuery : IRequest<Result<List<StockTakeDto>>> { }

    public class GetStockTakesHandler : IRequestHandler<GetStockTakesQuery, Result<List<StockTakeDto>>>
    {
        private readonly IErpDbContext _context;
        public GetStockTakesHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<StockTakeDto>>> Handle(GetStockTakesQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.StockTakes
                .Include(s => s.Warehouse)
                .OrderByDescending(s => s.Id)
                .Select(s => new StockTakeDto
                {
                    Id = s.Id,
                    StockTakeNumber = s.StockTakeNumber,
                    Date = s.Date.ToString("yyyy-MM-dd"),
                    WarehouseName = s.Warehouse.Name,
                    StatusText = s.Status.ToString(),
                    TotalVarianceValue = s.TotalVarianceValue
                })
                .ToListAsync(cancellationToken);

            return Result<List<StockTakeDto>>.Success(data);
        }
    }
}
