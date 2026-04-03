using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.StockTakes.Commands
{
    public class ApproveStockTakeCommand : IRequest<Result<int>>
    {
        public int StockTakeId { get; set; }
        public string ApproverName { get; set; } = "System Admin";
    }

    public class ApproveStockTakeHandler : IRequestHandler<ApproveStockTakeCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly IJournalService _journalService;
        private readonly IFinancialAccountResolver _accountResolver;

        public ApproveStockTakeHandler(IFinancialAccountResolver accountResolver, IErpDbContext context, IStockService stockService, IJournalService journalService)
        {
            _context = context;
            _stockService = stockService;
            _journalService = journalService;
            _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(ApproveStockTakeCommand request, CancellationToken cancellationToken)
        {
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                var stockTake = await _context.StockTakes
                    .Include(s => s.Items)
                        .ThenInclude(i => i.ProductVariant)
                            .ThenInclude(v => v.Product)
                                .ThenInclude(p => p.Category)
                    .FirstOrDefaultAsync(s => s.Id == request.StockTakeId, cancellationToken);

                if (stockTake == null) return Result<int>.Failure("Stock Take not found.");
                if (stockTake.Status != StockTakeStatus.Counted) return Result<int>.Failure("Only 'Counted' stock takes can be approved.");

                // Group GL Postings
                var inventoryAssetGroup = new Dictionary<int, decimal>(); // Key: AccountId, Value: Net Change
                decimal totalShrinkageValue = 0;
                decimal totalSurplusValue = 0;

                foreach (var item in stockTake.Items.Where(i => i.VarianceQty != 0))
                {
                    int invAcc = item.ProductVariant.Product.Category.InventoryAccountId ?? 0;
                    if (invAcc == 0) throw new Exception($"Product '{item.ProductVariant.Product.Name}' is missing an Inventory Asset Account.");

                    if (!inventoryAssetGroup.ContainsKey(invAcc)) inventoryAssetGroup[invAcc] = 0;

                    if (item.VarianceQty < 0)
                    {
                        // SHRINKAGE (Missing items) -> Consume FIFO layers
                        decimal lossQty = Math.Abs(item.VarianceQty);

                        var stockResult = await _stockService.ConsumeStockAsync(
                            item.ProductVariantId,
                            stockTake.WarehouseId,
                            lossQty,
                            stockTake.StockTakeNumber);

                        if (!stockResult.Succeeded) throw new Exception($"Stock Engine Error: {stockResult.Message}");

                        decimal actualFifoLoss = stockResult.Data; // The actual O(1) evaluated cost of the missing items

                        // Overwrite the snapshot estimate with the exact FIFO loss for perfect GL balancing
                        item.VarianceValue = -actualFifoLoss;

                        inventoryAssetGroup[invAcc] -= actualFifoLoss;
                        totalShrinkageValue += actualFifoLoss;
                    }
                    else if (item.VarianceQty > 0)
                    {
                        // SURPLUS (Extra items found) -> Create a new Receipt layer
                        decimal actualFifoGain = item.VarianceQty * item.UnitCost;

                        var stockResult = await _stockService.RestoreStockAsync(
                            item.ProductVariantId,
                            stockTake.WarehouseId,
                            item.VarianceQty,
                            actualFifoGain,
                            stockTake.StockTakeNumber,
                            "Stock Take Surplus");

                        if (!stockResult.Succeeded) throw new Exception($"Stock Engine Error: {stockResult.Message}");

                        // Overwrite with actual math to be safe
                        item.VarianceValue = actualFifoGain;

                        inventoryAssetGroup[invAcc] += actualFifoGain;
                        totalSurplusValue += actualFifoGain;
                    }
                }

                // =====================================================================
                // GL POSTING ENGINE
                // =====================================================================
                if (totalShrinkageValue > 0 || totalSurplusValue > 0)
                {
                    var journalLines = new List<JournalLineRequest>();

                    // Handle the Asset Account changes
                    foreach (var asset in inventoryAssetGroup)
                    {
                        if (asset.Value > 0)
                            journalLines.Add(new JournalLineRequest { AccountId = asset.Key, Debit = asset.Value, Credit = 0, Note = $"Stock Take Surplus - {stockTake.StockTakeNumber}" });
                        else if (asset.Value < 0)
                            journalLines.Add(new JournalLineRequest { AccountId = asset.Key, Debit = 0, Credit = Math.Abs(asset.Value), Note = $"Stock Take Shrinkage - {stockTake.StockTakeNumber}" });
                    }

                    // Handle Expense / Income Accounts
                    // Handle Expense / Income Accounts
                    if (totalShrinkageValue > 0)
                    {
                        var shrinkConfig = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "Account.Inventory.Shrinkage", cancellationToken);
                        if (shrinkConfig == null) throw new Exception("GL Config Missing: 'Account.Inventory.Shrinkage'");

                        // ARCHITECTURAL FIX: Translate the human-readable Account Code to the Database Id
                        var shrinkAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == shrinkConfig.Value, cancellationToken);
                        if (shrinkAccount == null) throw new Exception($"Configured Shrinkage Account Code '{shrinkConfig.Value}' does not exist in the Chart of Accounts.");

                        journalLines.Add(new JournalLineRequest { AccountId = shrinkAccount.Id, Debit = totalShrinkageValue, Credit = 0, Note = $"Inventory Loss - {stockTake.StockTakeNumber}" });
                    }

                    if (totalSurplusValue > 0)
                    {

                        var surplusId = await _accountResolver.ResolveAccountIdAsync("Account.Inventory.Surplus", cancellationToken);
                        if (surplusId == null) throw new Exception("GL Config Missing: 'Account.Inventory.Surplus'");

                        // ARCHITECTURAL FIX: Translate the human-readable Account Code to the Database Id
                        if (surplusId == null) throw new Exception($"Configured Surplus Account Code '{surplusId}' does not exist in the Chart of Accounts.");

                        journalLines.Add(new JournalLineRequest { AccountId = surplusId, Debit = 0, Credit = totalSurplusValue, Note = $"Inventory Gain - {stockTake.StockTakeNumber}" });
                    }

                    var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                    {
                        Date = DateTime.UtcNow,
                        Description = $"Stock Take Variance Reconciliation: {stockTake.StockTakeNumber}",
                        Module = "Inventory",
                        ReferenceNo = stockTake.StockTakeNumber,
                        Lines = journalLines
                    });

                    if (!jResult.Succeeded) throw new Exception($"GL Posting Failed: {jResult.Message}");
                }

                stockTake.Status = StockTakeStatus.Approved;
                stockTake.ApprovedBy = request.ApproverName;
                stockTake.ApprovedAt = DateTime.UtcNow;
                stockTake.TotalVarianceValue = totalSurplusValue - totalShrinkageValue; // Net impact

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(stockTake.Id, "Stock Take approved successfully. Inventory and GL have been adjusted.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Approval Failed: {ex.Message}");
            }
        }
    }
}
