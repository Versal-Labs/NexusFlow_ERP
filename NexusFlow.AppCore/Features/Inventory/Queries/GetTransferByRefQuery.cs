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
    public class GetTransferByRefQuery : IRequest<Result<object>>
    {
        public string ReferenceNo { get; set; } = string.Empty;
    }

    public class GetTransferByRefHandler : IRequestHandler<GetTransferByRefQuery, Result<object>>
    {
        private readonly IErpDbContext _context;
        public GetTransferByRefHandler(IErpDbContext context) => _context = context;

        public async Task<Result<object>> Handle(GetTransferByRefQuery request, CancellationToken cancellationToken)
        {
            var txns = await _context.StockTransactions
                .Include(t => t.Warehouse)
                .Include(t => t.ProductVariant).ThenInclude(v => v!.Product)
                .Where(t => t.ReferenceDocNo == request.ReferenceNo)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (!txns.Any()) return Result<object>.Failure("Transfer not found.");

            var outTxns = txns.Where(t => t.Type == StockTransactionType.TransferOut).ToList();
            var inTxns = txns.Where(t => t.Type == StockTransactionType.TransferIn).ToList();

            var firstOut = outTxns.First();
            var firstIn = inTxns.FirstOrDefault();

            var data = new
            {
                ReferenceNo = firstOut.ReferenceDocNo,
                Date = firstOut.Date,
                SourceWarehouse = firstOut.Warehouse.Name,
                TargetWarehouse = firstIn?.Warehouse.Name ?? "Unknown",
                Notes = firstOut.Notes,
                TotalValue = outTxns.Sum(t => t.TotalValue),
                // Group items because FIFO might have split 1 item into 3 transactions (layers)
                Items = outTxns.GroupBy(t => new { t.ProductVariantId, t.ProductVariant!.Name, t.ProductVariant.SKU })
                    .Select(g => new
                    {
                        VariantId = g.Key.ProductVariantId,
                        Description = g.Key.Name,
                        Sku = g.Key.SKU,
                        Qty = g.Sum(t => t.Qty),
                        TotalValue = g.Sum(t => t.TotalValue)
                    }).ToList()
            };

            return Result<object>.Success(data);
        }
    }
}
