using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Commands
{
    public class TransferItemRequest
    {
        public int ProductVariantId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class TransferStockCommand : IRequest<Result<string>>
    {
        public int SourceWarehouseId { get; set; }
        public int TargetWarehouseId { get; set; }
        public DateTime TransferDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<TransferItemRequest> Items { get; set; } = new();
    }

    public class TransferStockHandler : IRequestHandler<TransferStockCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly INumberSequenceService _sequenceService;

        public TransferStockHandler(IErpDbContext context, IStockService stockService, INumberSequenceService sequenceService)
        {
            _context = context; _stockService = stockService; _sequenceService = sequenceService;
        }

        public async Task<Result<string>> Handle(TransferStockCommand request, CancellationToken cancellationToken)
        {
            if (request.SourceWarehouseId == request.TargetWarehouseId)
                return Result<string>.Failure("Source and Target warehouses cannot be the same.");

            if (!request.Items.Any())
                return Result<string>.Failure("You must select at least one item to transfer.");

            // Use a transaction so if one item fails (e.g., out of stock), the whole transfer rolls back.
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                string transferRef = await _sequenceService.GenerateNextNumberAsync("StockTransfer", cancellationToken);

                foreach (var item in request.Items.Where(i => i.Quantity > 0))
                {
                    // Calls your robust StockService which handles the strict FIFO layer generation
                    var transferResult = await _stockService.TransferStockAsync(
                        item.ProductVariantId,
                        request.SourceWarehouseId,
                        request.TargetWarehouseId,
                        item.Quantity,
                        transferRef,
                        request.Notes);

                    if (!transferResult.Succeeded)
                        throw new Exception($"Transfer failed for Variant ID {item.ProductVariantId}: {transferResult.Message}");
                }

                await transaction.CommitAsync(cancellationToken);
                return Result<string>.Success(transferRef, $"Transfer {transferRef} executed successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<string>.Failure($"Transfer Failed: {ex.Message}");
            }
        }
    }
}
