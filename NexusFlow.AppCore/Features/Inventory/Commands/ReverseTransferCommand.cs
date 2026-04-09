using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Commands
{
    public class ReverseTransferCommand : IRequest<Result<string>>
    {
        public string ReferenceNo { get; set; } = string.Empty;
    }

    public class ReverseTransferHandler : IRequestHandler<ReverseTransferCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly INumberSequenceService _sequenceService;

        public ReverseTransferHandler(IErpDbContext context, IStockService stockService, INumberSequenceService sequenceService)
        {
            _context = context; _stockService = stockService; _sequenceService = sequenceService;
        }

        public async Task<Result<string>> Handle(ReverseTransferCommand request, CancellationToken cancellationToken)
        {
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                var txns = await _context.StockTransactions
                    .Where(t => t.ReferenceDocNo == request.ReferenceNo)
                    .ToListAsync(cancellationToken);

                if (!txns.Any()) return Result<string>.Failure("Transfer not found.");

                // Check if already reversed (prevent infinite reversal loops)
                if (txns.First().Notes.StartsWith("Reversal of"))
                    return Result<string>.Failure("Cannot reverse a transfer that is already a reversal.");

                var outTxns = txns.Where(t => t.Type == StockTransactionType.TransferOut).ToList();
                var inTxns = txns.Where(t => t.Type == StockTransactionType.TransferIn).ToList();

                int originalSourceWh = outTxns.First().WarehouseId;
                int originalTargetWh = inTxns.First().WarehouseId;

                string newRef = await _sequenceService.GenerateNextNumberAsync("StockTransfer", cancellationToken);
                string notes = $"Reversal of {request.ReferenceNo}";

                // Group items to determine exact quantities to send back
                var itemsToReverse = outTxns.GroupBy(t => t.ProductVariantId)
                    .Select(g => new { VariantId = g.Key, Qty = g.Sum(t => t.Qty) }).ToList();

                foreach (var item in itemsToReverse)
                {
                    // Attempt to move stock back from the Target to the Source
                    var res = await _stockService.TransferStockAsync(
                        item.VariantId, originalTargetWh, originalSourceWh, item.Qty, newRef, notes);

                    if (!res.Succeeded)
                        throw new Exception($"Cannot reverse. Target warehouse no longer has enough stock for Variant ID {item.VariantId}.");
                }

                await transaction.CommitAsync(cancellationToken);
                return Result<string>.Success(newRef, $"Transfer successfully reversed via new document: {newRef}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<string>.Failure(ex.Message);
            }
        }
    }
}
