using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Purchasing.PurchaseOrders.Commands
{
    public class CreatePurchaseOrderCommand : IRequest<Result<int>>, IFinancialPeriodControlledRequest
    {
        public int SupplierId { get; set; }
        public DateTime Date { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string Note { get; set; } = string.Empty;
        public bool IsDraft { get; set; }
        public List<PurchaseOrderItemDto> Items { get; set; } = new();
        public DateTime FinancialDate => Date;
    }

    public class CreatePurchaseOrderHandler : IRequestHandler<CreatePurchaseOrderCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequenceService;

        public CreatePurchaseOrderHandler(IErpDbContext context, INumberSequenceService sequenceService)
        {
            _context = context;
            _sequenceService = sequenceService;
        }

        public async Task<Result<int>> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
        {
            if (request.Items == null || !request.Items.Any())
                return Result<int>.Failure("Cannot create an empty Purchase Order.");

            var validationResult = await ValidateItemsAsync(request.Items, request.IsDraft, cancellationToken);
            if (!validationResult.Succeeded)
                return validationResult;

            string poNumber = await _sequenceService.GenerateNextNumberAsync("Purchasing", cancellationToken);

            var po = new PurchaseOrder
            {
                PoNumber = poNumber,
                Date = request.Date,
                ExpectedDate = request.ExpectedDate,
                SupplierId = request.SupplierId,
                Status = request.IsDraft ? PurchaseOrderStatus.Draft : PurchaseOrderStatus.Approved,
                Items = new List<PurchaseOrderItem>(),
                Note = request.Note
            };

            decimal grandTotal = 0;

            foreach (var item in request.Items)
            {
                po.Items.Add(new PurchaseOrderItem
                {
                    ProductVariantId = item.ProductVariantId,
                    QuantityOrdered = item.QuantityOrdered,
                    UnitCost = item.UnitCost,
                    QuantityReceived = 0
                });

                grandTotal += item.QuantityOrdered * item.UnitCost;
            }

            po.TotalAmount = grandTotal;

            _context.PurchaseOrders.Add(po);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(po.Id, $"Purchase Order {poNumber} created successfully.");
        }

        private async Task<Result<int>> ValidateItemsAsync(List<PurchaseOrderItemDto> items, bool isDraft, CancellationToken cancellationToken)
        {
            if (items.Any(i => i.ProductVariantId <= 0))
                return Result<int>.Failure("Each Purchase Order line must have a product.");

            if (items.Any(i => i.QuantityOrdered <= 0))
                return Result<int>.Failure("Purchase Order quantities must be greater than zero.");

            if (items.Any(i => i.UnitCost <= 0))
                return Result<int>.Failure("Purchase Order unit cost must be greater than zero.");

            var variantIds = items.Select(i => i.ProductVariantId).Distinct().ToList();
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.Category)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken);

            foreach (var item in items)
            {
                if (!variants.TryGetValue(item.ProductVariantId, out var variant))
                    return Result<int>.Failure($"Product variant {item.ProductVariantId} was not found.");

                if (variant.Product.Type == ProductType.Service)
                    return Result<int>.Failure($"Service item '{variant.SKU}' cannot be purchased through stock PO/GRN.");

                var category = variant.Product.Category;
                var hasPostingAccount = (category?.InventoryAccountId ?? 0) > 0 || (category?.CogsAccountId ?? 0) > 0;
                if (!isDraft && !hasPostingAccount)
                    return Result<int>.Failure($"Product category for '{variant.SKU}' is missing an Inventory or Expense Account.");
            }

            return Result<int>.Success(0);
        }
    }
}
