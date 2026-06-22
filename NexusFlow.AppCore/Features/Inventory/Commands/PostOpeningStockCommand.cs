using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Inventory.Commands
{
    public class OpeningStockLineRequest
    {
        public int ProductVariantId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal? SellingPrice { get; set; }
        public string? BatchNo { get; set; }
    }

    public class PostOpeningStockCommand : IRequest<Result<string>>, IFinancialPeriodControlledRequest
    {
        public DateTime OpeningDate { get; set; } = DateTime.UtcNow;
        public int WarehouseId { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<OpeningStockLineRequest> Items { get; set; } = new();
        public DateTime FinancialDate => OpeningDate;
    }

    public class PostOpeningStockHandler : IRequestHandler<PostOpeningStockCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequenceService;
        private readonly IJournalService _journalService;
        private readonly IFinancialAccountResolver _accountResolver;

        public PostOpeningStockHandler(
            IErpDbContext context,
            INumberSequenceService sequenceService,
            IJournalService journalService,
            IFinancialAccountResolver accountResolver)
        {
            _context = context;
            _sequenceService = sequenceService;
            _journalService = journalService;
            _accountResolver = accountResolver;
        }

        public async Task<Result<string>> Handle(PostOpeningStockCommand request, CancellationToken cancellationToken)
        {
            if (request.Items == null || !request.Items.Any())
                return Result<string>.Failure("Opening stock requires at least one item.");

            if (request.WarehouseId <= 0)
                return Result<string>.Failure("Warehouse is required.");

            var warehouseExists = await _context.Warehouses
                .AnyAsync(w => w.Id == request.WarehouseId, cancellationToken);

            if (!warehouseExists)
                return Result<string>.Failure("Warehouse not found.");

            var normalizedItems = request.Items
                .Select(i => new OpeningStockLineRequest
                {
                    ProductVariantId = i.ProductVariantId,
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost,
                    SellingPrice = i.SellingPrice,
                    BatchNo = i.BatchNo?.Trim()
                })
                .ToList();

            var invalidQuantity = normalizedItems.FirstOrDefault(i => i.Quantity <= 0);
            if (invalidQuantity != null)
                return Result<string>.Failure("Opening stock quantity must be greater than zero.");

            if (normalizedItems.Any(i => i.Quantity % 1 != 0))
                return Result<string>.Failure("Opening stock quantity must be a whole number.");

            var invalidCost = normalizedItems.FirstOrDefault(i => i.UnitCost <= 0);
            if (invalidCost != null)
                return Result<string>.Failure("Opening stock unit cost must be greater than zero.");

            if (normalizedItems.Any(i => i.SellingPrice.HasValue && i.SellingPrice.Value < 0))
                return Result<string>.Failure("Selling price cannot be negative.");

            if (normalizedItems.Any(i => i.ProductVariantId <= 0))
                return Result<string>.Failure("Each opening stock line must have a product variant.");

            var duplicateVariantId = normalizedItems
                .GroupBy(i => i.ProductVariantId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .FirstOrDefault();

            if (duplicateVariantId > 0)
                return Result<string>.Failure("Duplicate variants are not allowed in the same opening stock entry.");

            var variantIds = normalizedItems.Select(i => i.ProductVariantId).Distinct().ToList();
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.Category)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken);

            var missingVariantId = variantIds.FirstOrDefault(id => !variants.ContainsKey(id));
            if (missingVariantId != 0)
                return Result<string>.Failure($"Product variant {missingVariantId} was not found.");

            foreach (var item in normalizedItems)
            {
                var variant = variants[item.ProductVariantId];
                var product = variant.Product;

                if (product.Type == ProductType.Service)
                    return Result<string>.Failure($"Service item '{variant.SKU}' cannot carry opening stock.");

                if (product.Category == null || !product.Category.InventoryAccountId.HasValue || product.Category.InventoryAccountId.Value <= 0)
                    return Result<string>.Failure($"Product category for '{variant.SKU}' is missing an Inventory Account.");
            }

            var existingStockVariantIds = await _context.StockTransactions
                .Where(t => t.WarehouseId == request.WarehouseId && variantIds.Contains(t.ProductVariantId))
                .Select(t => t.ProductVariantId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (existingStockVariantIds.Any())
            {
                var skus = existingStockVariantIds
                    .Select(id => variants[id].SKU)
                    .OrderBy(s => s)
                    .ToList();

                return Result<string>.Failure($"Opening stock already exists or stock activity has started for: {string.Join(", ", skus)}.");
            }

            int openingBalanceEquityAccountId;
            try
            {
                openingBalanceEquityAccountId = await _accountResolver.ResolveAccountIdAsync(AccountMappingKeys.OpeningBalanceEquity, cancellationToken);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"Opening Balance Equity configuration is missing or invalid: {ex.Message}");
            }

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                var referenceNo = await _sequenceService.GenerateNextNumberAsync(NumberSequenceKeys.OpeningStock, cancellationToken);
                var inventoryDebits = new Dictionary<int, decimal>();
                decimal totalOpeningValue = 0;
                var memo = string.IsNullOrWhiteSpace(request.Notes) ? "Opening Stock" : request.Notes.Trim();

                foreach (var item in normalizedItems)
                {
                    var variant = variants[item.ProductVariantId];
                    var inventoryAccountId = variant.Product.Category.InventoryAccountId!.Value;
                    var lineValue = item.Quantity * item.UnitCost;
                    var batchNo = string.IsNullOrWhiteSpace(item.BatchNo) ? referenceNo : item.BatchNo!;

                    variant.CostPrice = item.UnitCost;
                    variant.MovingAverageCost = item.UnitCost;
                    if (item.SellingPrice.HasValue)
                        variant.SellingPrice = item.SellingPrice.Value;

                    _context.StockLayers.Add(new StockLayer
                    {
                        ProductVariantId = item.ProductVariantId,
                        WarehouseId = request.WarehouseId,
                        BatchNo = batchNo,
                        DateReceived = request.OpeningDate,
                        UnitCost = item.UnitCost,
                        InitialQty = item.Quantity,
                        RemainingQty = item.Quantity,
                        IsExhausted = false
                    });

                    _context.StockTransactions.Add(new StockTransaction
                    {
                        Date = request.OpeningDate,
                        ProductVariantId = item.ProductVariantId,
                        WarehouseId = request.WarehouseId,
                        Type = StockTransactionType.OpeningBalance,
                        Qty = item.Quantity,
                        UnitCost = item.UnitCost,
                        TotalValue = lineValue,
                        ReferenceDocNo = referenceNo,
                        Notes = memo
                    });

                    if (!inventoryDebits.ContainsKey(inventoryAccountId))
                        inventoryDebits[inventoryAccountId] = 0;

                    inventoryDebits[inventoryAccountId] += lineValue;
                    totalOpeningValue += lineValue;
                }

                await _context.SaveChangesAsync(cancellationToken);

                var journalLines = inventoryDebits
                    .OrderBy(x => x.Key)
                    .Select(x => new JournalLineRequest
                    {
                        AccountId = x.Key,
                        Debit = x.Value,
                        Credit = 0,
                        Note = $"Opening Stock Inventory - {referenceNo}"
                    })
                    .ToList();

                journalLines.Add(new JournalLineRequest
                {
                    AccountId = openingBalanceEquityAccountId,
                    Debit = 0,
                    Credit = totalOpeningValue,
                    Note = $"Opening Stock Equity Offset - {referenceNo}"
                });

                var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = request.OpeningDate,
                    Description = $"Opening Stock: {referenceNo}",
                    Module = "Inventory",
                    ReferenceNo = referenceNo,
                    Lines = journalLines
                });

                if (!journalResult.Succeeded)
                    throw new InvalidOperationException(journalResult.Message ?? journalResult.Errors?.FirstOrDefault() ?? "GL posting failed.");

                await transaction.CommitAsync(cancellationToken);
                return Result<string>.Success(referenceNo, $"Opening stock {referenceNo} posted successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<string>.Failure($"Opening stock failed: {ex.Message}");
            }
        }
    }
}
