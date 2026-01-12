using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Commands
{
    public class RunProductionHandler : IRequestHandler<RunProductionCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly IJournalService _journalService;

        public RunProductionHandler(IErpDbContext context, IStockService stockService, IJournalService journalService)
        {
            _context = context;
            _stockService = stockService;
            _journalService = journalService;
        }

        public async Task<Result<int>> Handle(RunProductionCommand request, CancellationToken cancellationToken)
        {
            // 1. Fetch the BOM for this Finished Good
            var bom = await _context.BillOfMaterials
                .Include(b => b.Components)
                .FirstOrDefaultAsync(b => b.ProductVariantId == request.FinishedGoodVariantId && b.IsActive, cancellationToken);

            if (bom == null)
                return Result<int>.Failure("No Active Bill of Materials (BOM) found for this product.");

            decimal totalMaterialCost = 0;

            // 2. Loop through Ingredients (Backflushing)
            foreach (var component in bom.Components)
            {
                // Calculate Total Required (e.g., 1.5m * 100 Jeans = 150m)
                decimal qtyRequired = component.Quantity * request.QtyProduced;

                // Consume from Factory Warehouse
                var consumeResult = await _stockService.ConsumeStockAsync(
                    component.MaterialVariantId,
                    request.FactoryWarehouseId,
                    qtyRequired,
                    request.ReferenceDoc
                );

                if (!consumeResult.Succeeded)
                    return Result<int>.Failure($"Production Failed: {consumeResult.Message}");

                totalMaterialCost += consumeResult.Data;
            }

            // 3. Calculate Final Cost of Finished Goods
            // Formula: (Material Cost + Sewing Cost) / Qty Produced
            decimal totalProductionCost = totalMaterialCost + request.TotalServiceCost;
            decimal unitCostOfFinishedGood = totalProductionCost / request.QtyProduced;

            // 4. Receive Finished Goods into Stock
            await _stockService.ReceiveStockAsync(
                request.FinishedGoodVariantId,
                request.TargetWarehouseId,
                request.QtyProduced,
                unitCostOfFinishedGood,
                request.ReferenceDoc
            );

            var configKeys = new[] {
        "Account.Inventory.FinishedGood",
        "Account.Inventory.RawMaterial",
        "Account.Liability.ServiceAccrual"
    };

            var accountConfigs = await _context.SystemConfigs
                .Where(c => configKeys.Contains(c.Key))
                .ToDictionaryAsync(c => c.Key, c => c.Value, cancellationToken);

            // B. Validate Configurations (Fail if Admin hasn't set them up)
            if (!accountConfigs.ContainsKey("Account.Inventory.FinishedGood") ||
                !accountConfigs.ContainsKey("Account.Inventory.RawMaterial") ||
                !accountConfigs.ContainsKey("Account.Liability.ServiceAccrual"))
            {
                return Result<int>.Failure("GL Posting Failed: Missing Account Configurations. Please check System Settings.");
            }

            // C. Parse IDs safely
            int fgAccountId = int.Parse(accountConfigs["Account.Inventory.FinishedGood"]);
            int rmAccountId = int.Parse(accountConfigs["Account.Inventory.RawMaterial"]);
            int serviceLiabilityId = int.Parse(accountConfigs["Account.Liability.ServiceAccrual"]);

            // D. Create the Journal Entry
            var journalRequest = new JournalEntryRequest
            {
                Date = DateTime.UtcNow,
                Description = $"Production Run: {request.ReferenceDoc}",
                Module = "Inventory",
                ReferenceNo = request.ReferenceDoc,
                Lines = new List<JournalLineRequest>
                {
                    // DEBIT Finished Goods
                    new() { AccountId = fgAccountId, Debit = totalProductionCost, Credit = 0 },

                    // CREDIT Raw Materials
                    new() { AccountId = rmAccountId, Debit = 0, Credit = totalMaterialCost },

                    // CREDIT Service Liability
                    new() { AccountId = serviceLiabilityId, Debit = 0, Credit = request.TotalServiceCost }
                }
            };

            // 2. Delegate to the Engine
            var journalResult = await _journalService.PostJournalAsync(journalRequest);

            if (!journalResult.Succeeded)
            {
                // Important: In a real system, you might want to ROLLBACK the stock movement here if GL fails.
                // For now, we return the error.
                return Result<int>.Failure($"Stock Moved, but GL Posting Failed: {journalResult.Message}");
            }

            return Result<int>.Success(0, $"Production Complete. 1 Unit Cost = {unitCostOfFinishedGood:C2} (Mat: {totalMaterialCost} + Svc: {request.TotalServiceCost})");
        }
    }
}
