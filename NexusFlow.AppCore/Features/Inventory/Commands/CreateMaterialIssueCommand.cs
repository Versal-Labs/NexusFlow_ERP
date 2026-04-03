using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Commands
{
    public record CreateMaterialIssueCommand : IRequest<Result<string>>
    {
        public DateTime IssueDate { get; init; } = DateTime.UtcNow;
        public int SubcontractorId { get; init; } // The Garment Factory (Supplier)
        public int WarehouseId { get; init; } // Source of RM
        public int FinishedGoodVariantId { get; init; }
        public decimal TargetQuantity { get; init; }
        public string ReferenceNumber { get; init; } = string.Empty;
    }

    public class CreateMaterialIssueHandler : IRequestHandler<CreateMaterialIssueCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public CreateMaterialIssueHandler(IErpDbContext context, INumberSequenceService sequenceService, IFinancialAccountResolver accountResolver)
        {
            _context = context;
            _sequenceService = sequenceService;
            _accountResolver = accountResolver;
        }

        public async Task<Result<string>> Handle(CreateMaterialIssueCommand request, CancellationToken cancellationToken)
        {
            // 1. Fetch the BOM for the Target Finished Good
            var bom = await _context.BillOfMaterials
                .Include(b => b.Components)
                .FirstOrDefaultAsync(b => b.ProductVariantId == request.FinishedGoodVariantId && b.IsActive, cancellationToken);

            if (bom == null)
                return Result<string>.Failure("No active Bill of Materials found for this Product Variant.");

            // 2. Generate Reference Document No using your specific Interface method
            string issueNo = await _sequenceService.GenerateNextNumberAsync("MaterialIssue", cancellationToken);
            decimal totalMaterialCost = 0m;

            // 3. Execute STRICT FIFO for each BOM Component
            foreach (var component in bom.Components)
            {
                decimal requiredQty = component.Quantity * request.TargetQuantity;
                decimal qtyRemainingToFulfill = requiredQty;
                decimal totalCostForThisComponent = 0m;

                // Fetch available StockLayers for this RM in the specified Warehouse (FIFO)
                var availableLayers = await _context.StockLayers
                    .Where(sl => sl.ProductVariantId == component.MaterialVariantId
                              && sl.WarehouseId == request.WarehouseId
                              && sl.RemainingQty > 0)
                    .OrderBy(sl => sl.CreatedAt)
                    .ToListAsync(cancellationToken);

                foreach (var layer in availableLayers)
                {
                    if (qtyRemainingToFulfill <= 0) break;

                    decimal qtyToTake = Math.Min(layer.RemainingQty, qtyRemainingToFulfill);

                    layer.RemainingQty -= qtyToTake;
                    qtyRemainingToFulfill -= qtyToTake;

                    totalCostForThisComponent += (qtyToTake * layer.UnitCost);
                }

                if (qtyRemainingToFulfill > 0)
                    return Result<string>.Failure($"Insufficient stock in selected warehouse for Material Variant ID {component.MaterialVariantId}. Shortage: {qtyRemainingToFulfill}");

                // Create per-item StockTransaction as defined by your schema
                var stockTransaction = new StockTransaction
                {
                    Date = request.IssueDate,
                    ProductVariantId = component.MaterialVariantId,
                    WarehouseId = request.WarehouseId,
                    Type = StockTransactionType.TransferOut,
                    Qty = requiredQty,
                    UnitCost = totalCostForThisComponent / requiredQty, // Average FIFO unit cost
                    TotalValue = totalCostForThisComponent,
                    ReferenceDocNo = issueNo,
                    Notes = $"BOM Auto-Issue for {request.TargetQuantity} units of FG Variant {request.FinishedGoodVariantId}. Subcontractor: {request.SubcontractorId}. Ref: {request.ReferenceNumber}"
                };

                await _context.StockTransactions.AddAsync(stockTransaction, cancellationToken);
                totalMaterialCost += totalCostForThisComponent;
            }

            // 4. Real-time GL Posting exactly matching your JournalEntry schema
            // Note: Replace "RM_INV" and "WIP_INV" with your actual system lookup codes
            int rmInventoryAccountId = await _accountResolver.ResolveAccountIdAsync("RM_INV");
            int wipInventoryAccountId = await _accountResolver.ResolveAccountIdAsync("WIP_INV");

            var journalEntry = new JournalEntry
            {
                Date = request.IssueDate,
                Description = $"Material Issue {issueNo} to Subcontractor",
                Module = "Inventory",
                ReferenceNo = issueNo,
                TotalAmount = totalMaterialCost,
                Lines = new List<JournalLine>
            {
                new JournalLine { AccountId = wipInventoryAccountId, Debit = totalMaterialCost, Credit = 0 },
                new JournalLine { AccountId = rmInventoryAccountId, Debit = 0, Credit = totalMaterialCost }
            }
            };

            await _context.JournalEntries.AddAsync(journalEntry, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<string>.Success(issueNo, $"Material Issue {issueNo} generated successfully. Total Cost: {totalMaterialCost:C2}");
        }
    }
}
