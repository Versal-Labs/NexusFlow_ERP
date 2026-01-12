using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Commands
{
    public class TransferStockHandler : IRequestHandler<TransferStockCommand, Result>
    {
        private readonly IStockService _stockService;

        public TransferStockHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task<Result> Handle(TransferStockCommand request, CancellationToken cancellationToken)
        {
            foreach (var item in request.Items)
            {
                var result = await _stockService.TransferStockAsync(
                    item.ProductVariantId,
                    request.SourceWarehouseId,
                    request.TargetWarehouseId,
                    item.Qty,
                    request.ReferenceDoc
                );

                if (!result.Succeeded)
                {
                    // In a real app, you might want to rollback the whole transaction here
                    return Result.Failure($"Failed to transfer item {item.ProductVariantId}: {result.Message}");
                }
            }

            return Result.Success("Stock transfer completed successfully.");
        }
    }
}
