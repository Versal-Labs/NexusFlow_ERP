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
    public enum AdjustmentType { Shrinkage = 1, Surplus = 2 }

    public class AdjustmentItemRequest
    {
        public int ProductVariantId { get; set; }
        public AdjustmentType Type { get; set; }
        public decimal Quantity { get; set; } // Always positive in payload
        public decimal UnitCost { get; set; } // Required only for Surplus (to create the new FIFO layer)
    }

    public class AdjustStockCommand : IRequest<Result<string>>
    {
        public int WarehouseId { get; set; }
        public DateTime AdjustmentDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<AdjustmentItemRequest> Items { get; set; } = new();
    }

    public class AdjustStockHandler : IRequestHandler<AdjustStockCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public AdjustStockHandler(IErpDbContext context, IStockService stockService, IJournalService journalService, INumberSequenceService sequenceService, IFinancialAccountResolver accountResolver)
        {
            _context = context; _stockService = stockService; _journalService = journalService;
            _sequenceService = sequenceService; _accountResolver = accountResolver;
        }

        public async Task<Result<string>> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
        {
            if (!request.Items.Any()) return Result<string>.Failure("No items selected for adjustment.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            try
            {
                string adjRef = await _sequenceService.GenerateNextNumberAsync("StockAdjustment", cancellationToken);
                var journalLines = new List<JournalLineRequest>();

                // Resolve global GL Accounts for Adjustments
                int shrinkageAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Inventory.Shrinkage", cancellationToken);
                int surplusAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Inventory.Surplus", cancellationToken);

                // Group GL entries by Inventory Account (since different items have different asset accounts)
                var inventoryDebits = new Dictionary<int, decimal>();
                var inventoryCredits = new Dictionary<int, decimal>();
                decimal totalShrinkageExpense = 0;
                decimal totalSurplusRevenue = 0;

                foreach (var item in request.Items.Where(i => i.Quantity > 0))
                {
                    // Fetch category to know which Inventory Asset GL to hit
                    var variant = await _context.ProductVariants
                        .Include(v => v.Product).ThenInclude(p => p.Category)
                        .FirstOrDefaultAsync(v => v.Id == item.ProductVariantId, cancellationToken);

                    if (variant == null) throw new Exception("Variant not found.");
                    int invAccId = variant.Product.Category.InventoryAccountId ?? 0;

                    if (item.Type == AdjustmentType.Shrinkage)
                    {
                        // 1. SHRINKAGE: Deduct via FIFO and capture EXACT cost lost
                        decimal exactFifoLoss = await _stockService.IssueStockAsync(
                            item.ProductVariantId, request.WarehouseId, item.Quantity, adjRef, $"Shrinkage: {request.Reason}");

                        totalShrinkageExpense += exactFifoLoss;
                        if (!inventoryCredits.ContainsKey(invAccId)) inventoryCredits[invAccId] = 0;
                        inventoryCredits[invAccId] += exactFifoLoss;
                    }
                    else if (item.Type == AdjustmentType.Surplus)
                    {
                        // 2. SURPLUS: Create a new FIFO layer using the user-provided Unit Cost
                        if (item.UnitCost <= 0) throw new Exception($"Unit Cost is required for Surplus of {variant.SKU}.");

                        var receiveResult = await _stockService.ReceiveStockAsync(
                            item.ProductVariantId, request.WarehouseId, item.Quantity, item.UnitCost, adjRef, $"Surplus: {request.Reason}");

                        if (!receiveResult.Succeeded) throw new Exception(receiveResult.Message);

                        decimal exactSurplusGain = item.Quantity * item.UnitCost;
                        totalSurplusRevenue += exactSurplusGain;

                        if (!inventoryDebits.ContainsKey(invAccId)) inventoryDebits[invAccId] = 0;
                        inventoryDebits[invAccId] += exactSurplusGain;
                    }
                }

                // ==========================================
                // BUILD THE JOURNAL ENTRY
                // ==========================================
                // Shrinkage GL entries
                if (totalShrinkageExpense > 0)
                    journalLines.Add(new JournalLineRequest { AccountId = shrinkageAccountId, Debit = totalShrinkageExpense, Credit = 0, Note = $"Shrinkage Expense - {adjRef}" });

                foreach (var kvp in inventoryCredits)
                    journalLines.Add(new JournalLineRequest { AccountId = kvp.Key, Debit = 0, Credit = kvp.Value, Note = $"Shrinkage Asset Reduction - {adjRef}" });

                // Surplus GL entries
                if (totalSurplusRevenue > 0)
                    journalLines.Add(new JournalLineRequest { AccountId = surplusAccountId, Debit = 0, Credit = totalSurplusRevenue, Note = $"Surplus Gain - {adjRef}" });

                foreach (var kvp in inventoryDebits)
                    journalLines.Add(new JournalLineRequest { AccountId = kvp.Key, Debit = kvp.Value, Credit = 0, Note = $"Surplus Asset Addition - {adjRef}" });

                var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = request.AdjustmentDate,
                    Description = $"Stock Adjustment: {request.Reason}",
                    Module = "Inventory",
                    ReferenceNo = adjRef,
                    Lines = journalLines
                });

                if (!jResult.Succeeded) throw new Exception($"GL Posting Failed: {jResult.Message}");

                await transaction.CommitAsync(cancellationToken);
                return Result<string>.Success(adjRef, $"Adjustment {adjRef} executed and posted to GL.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<string>.Failure($"Adjustment Failed: {ex.Message}");
            }
        }
    }
}
