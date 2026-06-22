using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Inventory.ProductionOrders
{
    public class CreateProductionOrderCommand : IRequest<Result<int>>, IFinancialPeriodControlledRequest
    {
        public DateTime OrderDate { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int ContractorId { get; set; }
        public int FinishedGoodVariantId { get; set; }
        public int BillOfMaterialId { get; set; }
        public int SourceWarehouseId { get; set; }
        public int DestinationWarehouseId { get; set; }
        public decimal TargetQuantity { get; set; }
        public decimal? OverproductionTolerancePercent { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime FinancialDate => OrderDate;
    }

    public class UpdateProductionOrderCommand : CreateProductionOrderCommand
    {
        public int Id { get; set; }
        public string RowVersion { get; set; } = string.Empty;
    }

    public record ReleaseProductionOrderCommand(int Id, DateTime ReleaseDate) : IRequest<Result>, IFinancialPeriodControlledRequest
    {
        public DateTime FinancialDate => ReleaseDate;
    }

    public record ReviseProductionOrderCommand(int Id, DateTime RevisionDate, decimal TargetQuantity, decimal TolerancePercent, string Reason)
        : IRequest<Result>, IFinancialPeriodControlledRequest
    {
        public DateTime FinancialDate => RevisionDate;
    }

    public class ProductionOrderSaveHandler :
        IRequestHandler<CreateProductionOrderCommand, Result<int>>,
        IRequestHandler<UpdateProductionOrderCommand, Result<int>>,
        IRequestHandler<ReleaseProductionOrderCommand, Result>,
        IRequestHandler<ReviseProductionOrderCommand, Result>
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequences;

        public ProductionOrderSaveHandler(IErpDbContext context, INumberSequenceService sequences)
        {
            _context = context;
            _sequences = sequences;
        }

        public async Task<Result<int>> Handle(CreateProductionOrderCommand request, CancellationToken cancellationToken)
        {
            var validation = await ValidateHeaderAsync(request, cancellationToken);
            if (validation != null) return Result<int>.Failure(validation);

            var tolerance = request.OverproductionTolerancePercent ?? await DefaultToleranceAsync(cancellationToken);
            var order = new ProductionOrder
            {
                OrderNumber = await _sequences.GenerateNextNumberAsync(NumberSequenceKeys.ProductionOrder, cancellationToken),
                OrderDate = request.OrderDate,
                PlannedStartDate = request.PlannedStartDate,
                DueDate = request.DueDate,
                ContractorId = request.ContractorId,
                FinishedGoodVariantId = request.FinishedGoodVariantId,
                BillOfMaterialId = request.BillOfMaterialId,
                SourceWarehouseId = request.SourceWarehouseId,
                DestinationWarehouseId = request.DestinationWarehouseId,
                TargetQuantity = request.TargetQuantity,
                OverproductionTolerancePercent = tolerance,
                Notes = request.Notes,
                Status = ProductionOrderStatus.Draft
            };
            _context.ProductionOrders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(order.Id, $"Production order {order.OrderNumber} created as draft.");
        }

        public async Task<Result<int>> Handle(UpdateProductionOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _context.ProductionOrders.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (order == null) return Result<int>.Failure("Production order not found.");
            if (order.Status != ProductionOrderStatus.Draft) return Result<int>.Failure("Only draft production orders can be edited.");
            if (!string.IsNullOrWhiteSpace(request.RowVersion) && !order.RowVersion.SequenceEqual(Convert.FromBase64String(request.RowVersion)))
                return Result<int>.Failure("This production order was changed by another user. Reload and try again.");

            var validation = await ValidateHeaderAsync(request, cancellationToken);
            if (validation != null) return Result<int>.Failure(validation);
            order.OrderDate = request.OrderDate;
            order.PlannedStartDate = request.PlannedStartDate;
            order.DueDate = request.DueDate;
            order.ContractorId = request.ContractorId;
            order.FinishedGoodVariantId = request.FinishedGoodVariantId;
            order.BillOfMaterialId = request.BillOfMaterialId;
            order.SourceWarehouseId = request.SourceWarehouseId;
            order.DestinationWarehouseId = request.DestinationWarehouseId;
            order.TargetQuantity = request.TargetQuantity;
            order.OverproductionTolerancePercent = request.OverproductionTolerancePercent ?? order.OverproductionTolerancePercent;
            order.Notes = request.Notes;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(order.Id, $"Production order {order.OrderNumber} updated.");
        }

        public async Task<Result> Handle(ReleaseProductionOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _context.ProductionOrders
                .Include(x => x.Components)
                .Include(x => x.BillOfMaterial).ThenInclude(x => x.Components)
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (order == null) return Result.Failure("Production order not found.");
            if (order.Status != ProductionOrderStatus.Draft) return Result.Failure("Only draft production orders can be released.");
            if (!order.BillOfMaterial.IsApproved || !order.BillOfMaterial.IsActive)
                return Result.Failure("The selected BOM revision is not active and approved.");

            // The snapshot is the contractual recipe for this order; later BOM revisions cannot alter it.
            foreach (var item in order.BillOfMaterial.Components)
            {
                var perUnit = item.Quantity / order.BillOfMaterial.BasisQuantity;
                order.Components.Add(new ProductionOrderComponent
                {
                    MaterialVariantId = item.MaterialVariantId,
                    QuantityPerUnit = perUnit,
                    PlannedQuantity = perUnit * order.TargetQuantity
                });
            }
            if (order.Components.Count == 0) return Result.Failure("The approved BOM has no components.");
            order.BomRevisionNumber = order.BillOfMaterial.RevisionNumber;
            order.Status = ProductionOrderStatus.Released;
            order.ReleasedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success($"Production order {order.OrderNumber} released with BOM revision {order.BomRevisionNumber}.");
        }

        public async Task<Result> Handle(ReviseProductionOrderCommand request, CancellationToken cancellationToken)
        {
            if (request.TargetQuantity <= 0 || request.TargetQuantity != decimal.Truncate(request.TargetQuantity))
                return Result.Failure("Finished-goods target quantity must be a positive whole number.");
            if (request.TolerancePercent < 0) return Result.Failure("Tolerance cannot be negative.");
            if (string.IsNullOrWhiteSpace(request.Reason)) return Result.Failure("Revision reason is required.");

            var order = await _context.ProductionOrders.Include(x => x.Components).Include(x => x.Revisions)
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (order == null) return Result.Failure("Production order not found.");
            if (order.Status is ProductionOrderStatus.Closed or ProductionOrderStatus.Cancelled)
                return Result.Failure("Closed or cancelled orders cannot be revised.");

            foreach (var component in order.Components)
            {
                var newPlanned = component.QuantityPerUnit * request.TargetQuantity;
                if (component.IssuedQuantity > newPlanned)
                    return Result.Failure($"Target cannot be reduced below already issued material for variant {component.MaterialVariantId}.");
            }

            order.Revisions.Add(new ProductionOrderRevision
            {
                RevisionNumber = order.Revisions.Count + 1,
                RevisionDate = request.RevisionDate,
                PreviousTargetQuantity = order.TargetQuantity,
                NewTargetQuantity = request.TargetQuantity,
                PreviousTolerancePercent = order.OverproductionTolerancePercent,
                NewTolerancePercent = request.TolerancePercent,
                Reason = request.Reason
            });
            order.TargetQuantity = request.TargetQuantity;
            order.OverproductionTolerancePercent = request.TolerancePercent;
            foreach (var component in order.Components) component.PlannedQuantity = component.QuantityPerUnit * request.TargetQuantity;
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success("Production order revision recorded.");
        }

        private async Task<string?> ValidateHeaderAsync(CreateProductionOrderCommand request, CancellationToken cancellationToken)
        {
            if (request.TargetQuantity <= 0 || request.TargetQuantity != decimal.Truncate(request.TargetQuantity))
                return "Finished-goods target quantity must be a positive whole number.";
            if (request.SourceWarehouseId == request.DestinationWarehouseId)
                return "Source and finished-goods warehouses must be different.";
            var bom = await _context.BillOfMaterials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.BillOfMaterialId, cancellationToken);
            if (bom == null || !bom.IsApproved || !bom.IsActive || bom.ProductVariantId != request.FinishedGoodVariantId)
                return "Select the active approved BOM revision for the finished good.";
            var finishedGood = await _context.ProductVariants.Include(x => x.Product).AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.FinishedGoodVariantId, cancellationToken);
            if (finishedGood?.Product.Type != ProductType.FinishedGood) return "Production orders require a finished-good variant.";
            if (!await _context.Suppliers.AnyAsync(x => x.Id == request.ContractorId && x.IsActive, cancellationToken)) return "Contractor not found or inactive.";
            if (await _context.Warehouses.CountAsync(x => x.Id == request.SourceWarehouseId || x.Id == request.DestinationWarehouseId, cancellationToken) != 2)
                return "Select valid source and destination warehouses.";
            return null;
        }

        private async Task<decimal> DefaultToleranceAsync(CancellationToken cancellationToken)
        {
            var value = await _context.SystemConfigs.AsNoTracking()
                .Where(x => x.Key == ConfigurationKeys.ProductionOverproductionTolerancePercent)
                .Select(x => x.Value).FirstOrDefaultAsync(cancellationToken);
            return decimal.TryParse(value, out var tolerance) ? tolerance : 5m;
        }
    }

    public record ProductionMaterialLineRequest(int ProductionOrderComponentId, decimal Quantity);

    public class IssueProductionMaterialsCommand : IRequest<Result<string>>, IFinancialPeriodControlledRequest
    {
        public int ProductionOrderId { get; set; }
        public DateTime IssueDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<ProductionMaterialLineRequest> Lines { get; set; } = new();
        public DateTime FinancialDate => IssueDate;
    }

    public class ReturnProductionMaterialsCommand : IRequest<Result<string>>, IFinancialPeriodControlledRequest
    {
        public int ProductionOrderId { get; set; }
        public DateTime ReturnDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<ProductionMaterialLineRequest> Lines { get; set; } = new();
        public DateTime FinancialDate => ReturnDate;
    }

    public class ProductionMaterialHandler :
        IRequestHandler<IssueProductionMaterialsCommand, Result<string>>,
        IRequestHandler<ReturnProductionMaterialsCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequences;
        private readonly IFinancialAccountResolver _accounts;
        private readonly IJournalService _journals;

        public ProductionMaterialHandler(IErpDbContext context, INumberSequenceService sequences, IFinancialAccountResolver accounts, IJournalService journals)
        {
            _context = context;
            _sequences = sequences;
            _accounts = accounts;
            _journals = journals;
        }

        public async Task<Result<string>> Handle(IssueProductionMaterialsCommand request, CancellationToken cancellationToken)
        {
            if (request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0)) return Result<string>.Failure("Enter at least one positive issue quantity.");
            if (request.Lines.Select(x => x.ProductionOrderComponentId).Distinct().Count() != request.Lines.Count) return Result<string>.Failure("Duplicate material lines are not allowed.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            var order = await LoadOrderForMaterialsAsync(request.ProductionOrderId, cancellationToken);
            if (order == null) return Result<string>.Failure("Production order not found.");
            if (order.Status is not (ProductionOrderStatus.Released or ProductionOrderStatus.InProgress)) return Result<string>.Failure("Materials can only be issued to a released or in-progress order.");

            var reference = await _sequences.GenerateNextNumberAsync(NumberSequenceKeys.MaterialIssue, cancellationToken);
            var movement = new ProductionMaterialMovement
            {
                ProductionOrderId = order.Id, ReferenceNumber = reference, Date = request.IssueDate,
                Type = ProductionMaterialMovementType.Issue, WarehouseId = order.SourceWarehouseId, Notes = request.Notes
            };
            var inventoryCredits = new Dictionary<int, decimal>();

            foreach (var requested in request.Lines)
            {
                var component = order.Components.FirstOrDefault(x => x.Id == requested.ProductionOrderComponentId);
                if (component == null) return Result<string>.Failure("A material line does not belong to this production order.");
                if (component.IssuedQuantity + requested.Quantity > component.PlannedQuantity)
                    return Result<string>.Failure($"Issue quantity exceeds the approved plan for {component.MaterialVariant.SKU}.");

                var layers = await _context.StockLayers
                    .Where(x => x.ProductVariantId == component.MaterialVariantId && x.WarehouseId == order.SourceWarehouseId && x.RemainingQty > 0)
                    .OrderBy(x => x.DateReceived).ThenBy(x => x.Id).ToListAsync(cancellationToken);
                if (layers.Sum(x => x.RemainingQty) < requested.Quantity)
                    return Result<string>.Failure($"Insufficient stock for {component.MaterialVariant.SKU}.");

                var remaining = requested.Quantity;
                decimal cost = 0;
                foreach (var layer in layers)
                {
                    if (remaining <= 0) break;
                    var quantity = Math.Min(remaining, layer.RemainingQty);
                    layer.RemainingQty -= quantity;
                    layer.IsExhausted = layer.RemainingQty == 0;
                    remaining -= quantity;
                    cost += quantity * layer.UnitCost;
                }

                component.IssuedQuantity += requested.Quantity;
                component.IssuedCost += cost;
                movement.TotalCost += cost;
                movement.Lines.Add(new ProductionMaterialMovementLine
                {
                    ProductionOrderComponentId = component.Id, Quantity = requested.Quantity,
                    UnitCost = cost / requested.Quantity, TotalCost = cost
                });
                _context.StockTransactions.Add(new StockTransaction
                {
                    Date = request.IssueDate, ProductVariantId = component.MaterialVariantId, WarehouseId = order.SourceWarehouseId,
                    Type = StockTransactionType.TransferOut, Qty = requested.Quantity, UnitCost = cost / requested.Quantity,
                    TotalValue = cost, ReferenceDocNo = reference, Notes = $"Contractor WIP issue for {order.OrderNumber}"
                });
                var inventoryAccount = component.MaterialVariant.Product.Category.InventoryAccountId;
                if (!inventoryAccount.HasValue) return Result<string>.Failure($"Inventory account is missing for {component.MaterialVariant.SKU}.");
                inventoryCredits[inventoryAccount.Value] = inventoryCredits.GetValueOrDefault(inventoryAccount.Value) + cost;
            }

            order.Status = ProductionOrderStatus.InProgress;
            _context.ProductionMaterialMovements.Add(movement);
            var wipAccount = await _accounts.ResolveAccountIdAsync(AccountMappingKeys.WorkInProgressInventory, cancellationToken);
            var lines = new List<JournalLineRequest> { new() { AccountId = wipAccount, Debit = movement.TotalCost, Note = $"Contractor WIP {order.OrderNumber}" } };
            lines.AddRange(inventoryCredits.Select(x => new JournalLineRequest { AccountId = x.Key, Credit = x.Value, Note = $"Material issue {reference}" }));
            var journal = await _journals.PostJournalAsync(new JournalEntryRequest
            {
                Date = request.IssueDate, Module = "Production", ReferenceNo = reference,
                Description = $"Material issue for {order.OrderNumber}", Lines = lines
            });
            if (!journal.Succeeded) return Result<string>.Failure(journal.Message);
            await transaction.CommitAsync(cancellationToken);
            return Result<string>.Success(reference, $"Material issue {reference} posted at FIFO cost {movement.TotalCost:N2}.");
        }

        public async Task<Result<string>> Handle(ReturnProductionMaterialsCommand request, CancellationToken cancellationToken)
        {
            if (request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0)) return Result<string>.Failure("Enter at least one positive return quantity.");
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            var order = await LoadOrderForMaterialsAsync(request.ProductionOrderId, cancellationToken);
            if (order == null) return Result<string>.Failure("Production order not found.");
            if (order.Status != ProductionOrderStatus.InProgress) return Result<string>.Failure("Returns require an in-progress production order.");

            var reference = await _sequences.GenerateNextNumberAsync(NumberSequenceKeys.MaterialReturn, cancellationToken);
            var movement = new ProductionMaterialMovement
            {
                ProductionOrderId = order.Id, ReferenceNumber = reference, Date = request.ReturnDate,
                Type = ProductionMaterialMovementType.Return, WarehouseId = order.SourceWarehouseId, Notes = request.Notes
            };
            var inventoryDebits = new Dictionary<int, decimal>();
            foreach (var requested in request.Lines)
            {
                var component = order.Components.FirstOrDefault(x => x.Id == requested.ProductionOrderComponentId);
                if (component == null || requested.Quantity > component.ContractorHeldQuantity)
                    return Result<string>.Failure("Return quantity exceeds contractor-held material.");
                var heldQty = component.ContractorHeldQuantity;
                var unitCost = heldQty == 0 ? 0 : component.UnallocatedWipCost / heldQty;
                var cost = requested.Quantity * unitCost;
                component.ReturnedQuantity += requested.Quantity;
                component.ReturnedCost += cost;
                movement.TotalCost += cost;
                movement.Lines.Add(new ProductionMaterialMovementLine
                {
                    ProductionOrderComponentId = component.Id, Quantity = requested.Quantity, UnitCost = unitCost, TotalCost = cost
                });
                _context.StockLayers.Add(new StockLayer
                {
                    ProductVariantId = component.MaterialVariantId, WarehouseId = order.SourceWarehouseId,
                    BatchNo = reference, DateReceived = request.ReturnDate, InitialQty = requested.Quantity,
                    RemainingQty = requested.Quantity, UnitCost = unitCost, IsExhausted = false
                });
                _context.StockTransactions.Add(new StockTransaction
                {
                    Date = request.ReturnDate, ProductVariantId = component.MaterialVariantId, WarehouseId = order.SourceWarehouseId,
                    Type = StockTransactionType.TransferIn, Qty = requested.Quantity, UnitCost = unitCost, TotalValue = cost,
                    ReferenceDocNo = reference, Notes = $"Unused contractor material returned for {order.OrderNumber}"
                });
                var inventoryAccount = component.MaterialVariant.Product.Category.InventoryAccountId;
                if (!inventoryAccount.HasValue) return Result<string>.Failure($"Inventory account is missing for {component.MaterialVariant.SKU}.");
                inventoryDebits[inventoryAccount.Value] = inventoryDebits.GetValueOrDefault(inventoryAccount.Value) + cost;
            }

            _context.ProductionMaterialMovements.Add(movement);
            if (order.Receipts.Sum(x => x.AcceptedQuantity + x.RejectedQuantity) >= order.TargetQuantity &&
                order.Components.All(x => Math.Abs(x.ContractorHeldQuantity) < 0.0001m))
                order.Status = ProductionOrderStatus.ReadyToClose;
            var wipAccount = await _accounts.ResolveAccountIdAsync(AccountMappingKeys.WorkInProgressInventory, cancellationToken);
            var lines = inventoryDebits.Select(x => new JournalLineRequest { AccountId = x.Key, Debit = x.Value, Note = $"Material return {reference}" }).ToList();
            lines.Add(new JournalLineRequest { AccountId = wipAccount, Credit = movement.TotalCost, Note = $"Release contractor WIP {order.OrderNumber}" });
            var journal = await _journals.PostJournalAsync(new JournalEntryRequest
            {
                Date = request.ReturnDate, Module = "Production", ReferenceNo = reference,
                Description = $"Material return for {order.OrderNumber}", Lines = lines
            });
            if (!journal.Succeeded) return Result<string>.Failure(journal.Message);
            await transaction.CommitAsync(cancellationToken);
            return Result<string>.Success(reference, $"Material return {reference} posted.");
        }

        private async Task<ProductionOrder?> LoadOrderForMaterialsAsync(int id, CancellationToken cancellationToken)
        {
            return await _context.ProductionOrders
                .Include(x => x.Components).ThenInclude(x => x.MaterialVariant).ThenInclude(x => x.Product).ThenInclude(x => x.Category)
                .Include(x => x.Receipts)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
    }

    public record ProductionConsumptionRequest(
        int ProductionOrderComponentId,
        decimal ConsumedQuantity,
        decimal NormalWasteQuantity,
        decimal AbnormalLossQuantity,
        decimal ContractorRecoverableQuantity);

    public class ReceiveProductionOrderCommand : IRequest<Result<string>>, IFinancialPeriodControlledRequest
    {
        public int ProductionOrderId { get; set; }
        public DateTime ReceiptDate { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public decimal SewingCharge { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public List<ProductionConsumptionRequest> Consumptions { get; set; } = new();
        public DateTime FinancialDate => ReceiptDate;
    }

    public class ReceiveProductionOrderHandler : IRequestHandler<ReceiveProductionOrderCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequences;
        private readonly IFinancialAccountResolver _accounts;
        private readonly IJournalService _journals;

        public ReceiveProductionOrderHandler(IErpDbContext context, INumberSequenceService sequences, IFinancialAccountResolver accounts, IJournalService journals)
        {
            _context = context; _sequences = sequences; _accounts = accounts; _journals = journals;
        }

        public async Task<Result<string>> Handle(ReceiveProductionOrderCommand request, CancellationToken cancellationToken)
        {
            if (request.AcceptedQuantity < 0 || request.RejectedQuantity < 0 || request.AcceptedQuantity + request.RejectedQuantity <= 0)
                return Result<string>.Failure("Enter accepted or rejected output quantity.");
            if (request.AcceptedQuantity != decimal.Truncate(request.AcceptedQuantity) || request.RejectedQuantity != decimal.Truncate(request.RejectedQuantity))
                return Result<string>.Failure("Finished-goods accepted and rejected quantities must be whole numbers.");
            if (request.SewingCharge < 0) return Result<string>.Failure("Sewing charge cannot be negative.");
            if (request.Consumptions.Count == 0) return Result<string>.Failure("Actual component consumption is required.");
            if (request.Consumptions.Select(x => x.ProductionOrderComponentId).Distinct().Count() != request.Consumptions.Count)
                return Result<string>.Failure("Duplicate component consumption lines are not allowed.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            var order = await _context.ProductionOrders
                .Include(x => x.Contractor)
                .Include(x => x.FinishedGoodVariant).ThenInclude(x => x.Product).ThenInclude(x => x.Category)
                .Include(x => x.Components).ThenInclude(x => x.MaterialVariant)
                .Include(x => x.Receipts)
                .FirstOrDefaultAsync(x => x.Id == request.ProductionOrderId, cancellationToken);
            if (order == null) return Result<string>.Failure("Production order not found.");
            if (order.Status != ProductionOrderStatus.InProgress) return Result<string>.Failure("Receipts require an in-progress production order.");

            var maximumAccepted = order.TargetQuantity * (1 + order.OverproductionTolerancePercent / 100m);
            if (order.Receipts.Sum(x => x.AcceptedQuantity) + request.AcceptedQuantity > maximumAccepted)
                return Result<string>.Failure($"Accepted quantity exceeds the authorized {order.OverproductionTolerancePercent:N2}% tolerance. Revise the order first.");

            var receiptNumber = await _sequences.GenerateNextNumberAsync(NumberSequenceKeys.ProductionReceipt, cancellationToken);
            var receipt = new ProductionReceipt
            {
                ProductionOrderId = order.Id, ReceiptNumber = receiptNumber, ReceiptDate = request.ReceiptDate,
                AcceptedQuantity = request.AcceptedQuantity, RejectedQuantity = request.RejectedQuantity,
                SewingCharge = request.SewingCharge, BatchNumber = string.IsNullOrWhiteSpace(request.BatchNumber) ? receiptNumber : request.BatchNumber,
                Notes = request.Notes
            };

            foreach (var requested in request.Consumptions)
            {
                if (requested.ConsumedQuantity < 0 || requested.NormalWasteQuantity < 0 || requested.AbnormalLossQuantity < 0 || requested.ContractorRecoverableQuantity < 0)
                    return Result<string>.Failure("Consumption and loss quantities cannot be negative.");
                var component = order.Components.FirstOrDefault(x => x.Id == requested.ProductionOrderComponentId);
                if (component == null) return Result<string>.Failure("A consumption line does not belong to this order.");
                var allocatedQty = requested.ConsumedQuantity + requested.NormalWasteQuantity + requested.AbnormalLossQuantity + requested.ContractorRecoverableQuantity;
                if (allocatedQty > component.ContractorHeldQuantity) return Result<string>.Failure($"Allocated quantity exceeds contractor-held balance for {component.MaterialVariant.SKU}.");
                if (allocatedQty == 0) continue;

                // Every receipt draws only from the remaining WIP pool, so no issue cost can be capitalized twice.
                var unitCost = component.UnallocatedWipCost / component.ContractorHeldQuantity;
                var consumedCost = requested.ConsumedQuantity * unitCost;
                var normalCost = requested.NormalWasteQuantity * unitCost;
                var abnormalCost = requested.AbnormalLossQuantity * unitCost;
                var recoverableCost = requested.ContractorRecoverableQuantity * unitCost;
                component.ConsumedQuantity += requested.ConsumedQuantity;
                component.ConsumedCost += consumedCost;
                component.NormalWasteQuantity += requested.NormalWasteQuantity;
                component.NormalWasteCost += normalCost;
                component.AbnormalLossQuantity += requested.AbnormalLossQuantity;
                component.AbnormalLossCost += abnormalCost;
                component.ContractorRecoverableQuantity += requested.ContractorRecoverableQuantity;
                component.ContractorRecoverableCost += recoverableCost;
                receipt.MaterialCostCapitalized += consumedCost;
                receipt.NormalWasteCost += normalCost;
                receipt.AbnormalLossCost += abnormalCost;
                receipt.ContractorRecoverableCost += recoverableCost;
                receipt.Consumptions.Add(new ProductionReceiptConsumption
                {
                    ProductionOrderComponentId = component.Id,
                    ConsumedQuantity = requested.ConsumedQuantity, ConsumedCost = consumedCost,
                    NormalWasteQuantity = requested.NormalWasteQuantity, NormalWasteCost = normalCost,
                    AbnormalLossQuantity = requested.AbnormalLossQuantity, AbnormalLossCost = abnormalCost,
                    ContractorRecoverableQuantity = requested.ContractorRecoverableQuantity, ContractorRecoverableCost = recoverableCost
                });
            }

            var totalWipReleased = receipt.MaterialCostCapitalized + receipt.NormalWasteCost + receipt.AbnormalLossCost + receipt.ContractorRecoverableCost;
            if (totalWipReleased <= 0) return Result<string>.Failure("Receipt must allocate contractor-held material cost.");
            receipt.FinishedGoodsCost = receipt.MaterialCostCapitalized + receipt.NormalWasteCost + receipt.SewingCharge;

            if (request.AcceptedQuantity > 0)
            {
                var unitCost = receipt.FinishedGoodsCost / request.AcceptedQuantity;
                _context.StockLayers.Add(new StockLayer
                {
                    ProductVariantId = order.FinishedGoodVariantId, WarehouseId = order.DestinationWarehouseId,
                    BatchNo = receipt.BatchNumber, DateReceived = request.ReceiptDate, InitialQty = request.AcceptedQuantity,
                    RemainingQty = request.AcceptedQuantity, UnitCost = unitCost, IsExhausted = false
                });
                _context.StockTransactions.Add(new StockTransaction
                {
                    Date = request.ReceiptDate, ProductVariantId = order.FinishedGoodVariantId, WarehouseId = order.DestinationWarehouseId,
                    Type = StockTransactionType.ProductionIn, Qty = request.AcceptedQuantity, UnitCost = unitCost,
                    TotalValue = receipt.FinishedGoodsCost, ReferenceDocNo = receiptNumber, Notes = $"Production order {order.OrderNumber}"
                });
            }

            order.Receipts.Add(receipt);
            if (receipt.ContractorRecoverableCost > 0)
            {
                if (!order.Contractor.DefaultPayableAccountId.HasValue) return Result<string>.Failure("Contractor payable account is not configured.");
                _context.ProductionSupplierClaims.Add(new ProductionSupplierClaim
                {
                    ProductionOrderId = order.Id, ProductionReceipt = receipt, SupplierId = order.ContractorId,
                    ClaimNumber = await _sequences.GenerateNextNumberAsync(NumberSequenceKeys.DebitNote, cancellationToken),
                    ClaimDate = request.ReceiptDate, Amount = receipt.ContractorRecoverableCost,
                    Reason = $"Recoverable material loss on {receiptNumber}", Status = ProductionClaimStatus.Open
                });
            }

            var fgAccount = order.FinishedGoodVariant.Product.Category.InventoryAccountId;
            if (!fgAccount.HasValue) return Result<string>.Failure("Finished-good category inventory account is missing.");
            var wipAccount = await _accounts.ResolveAccountIdAsync(AccountMappingKeys.WorkInProgressInventory, cancellationToken);
            var serviceAccrual = await _accounts.ResolveAccountIdAsync(AccountMappingKeys.ServiceAccrual, cancellationToken);
            var lossAccount = await _accounts.ResolveAccountIdAsync(AccountMappingKeys.InventoryShrinkage, cancellationToken);
            var journalLines = new List<JournalLineRequest>();
            if (receipt.FinishedGoodsCost > 0) journalLines.Add(new() { AccountId = fgAccount.Value, Debit = receipt.FinishedGoodsCost, Note = $"Accepted output {receiptNumber}" });
            if (receipt.AbnormalLossCost > 0) journalLines.Add(new() { AccountId = lossAccount, Debit = receipt.AbnormalLossCost, Note = $"Abnormal production loss {receiptNumber}" });
            if (receipt.ContractorRecoverableCost > 0) journalLines.Add(new() { AccountId = order.Contractor.DefaultPayableAccountId!.Value, Debit = receipt.ContractorRecoverableCost, Note = $"Contractor recovery {receiptNumber}" });
            journalLines.Add(new() { AccountId = wipAccount, Credit = totalWipReleased, Note = $"Allocate WIP {order.OrderNumber}" });
            if (receipt.SewingCharge > 0) journalLines.Add(new() { AccountId = serviceAccrual, Credit = receipt.SewingCharge, Note = $"Sewing accrual {receiptNumber}" });
            var journal = await _journals.PostJournalAsync(new JournalEntryRequest
            {
                Date = request.ReceiptDate, Module = "Production", ReferenceNo = receiptNumber,
                Description = $"Production receipt for {order.OrderNumber}", Lines = journalLines
            });
            if (!journal.Succeeded) return Result<string>.Failure(journal.Message);

            await UpdateReadyStatusAsync(order, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<string>.Success(receiptNumber, $"Production receipt {receiptNumber} posted. Finished-goods cost {receipt.FinishedGoodsCost:N2}.");
        }

        private async Task UpdateReadyStatusAsync(ProductionOrder order, CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
            var output = order.Receipts.Sum(x => x.AcceptedQuantity + x.RejectedQuantity);
            if (output >= order.TargetQuantity && order.Components.All(x => Math.Abs(x.ContractorHeldQuantity) < 0.0001m))
                order.Status = ProductionOrderStatus.ReadyToClose;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public record CloseProductionOrderCommand(int Id, DateTime CloseDate) : IRequest<Result>, IFinancialPeriodControlledRequest
    {
        public DateTime FinancialDate => CloseDate;
    }

    public record SettleProductionClaimCommand(int ProductionOrderId, int ClaimId, DateTime SettlementDate, string SettlementReference)
        : IRequest<Result>, IFinancialPeriodControlledRequest
    {
        public DateTime FinancialDate => SettlementDate;
    }

    public class CloseProductionOrderHandler :
        IRequestHandler<CloseProductionOrderCommand, Result>,
        IRequestHandler<SettleProductionClaimCommand, Result>
    {
        private readonly IErpDbContext _context;
        public CloseProductionOrderHandler(IErpDbContext context) => _context = context;

        public async Task<Result> Handle(CloseProductionOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _context.ProductionOrders
                .Include(x => x.Components).Include(x => x.Receipts).Include(x => x.Revisions)
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (order == null) return Result.Failure("Production order not found.");
            if (order.Status != ProductionOrderStatus.ReadyToClose) return Result.Failure("Reconcile all output and contractor-held materials before closure.");
            if (order.Components.Any(x => Math.Abs(x.ContractorHeldQuantity) >= 0.0001m || Math.Abs(x.UnallocatedWipCost) >= 0.01m))
                return Result.Failure("Material quantities or WIP costs remain unreconciled.");
            if (order.Receipts.Any(x => x.SewingCharge > 0 && !x.SupplierBillId.HasValue))
                return Result.Failure("All sewing accruals must be matched to supplier bills before closure.");
            if (await _context.ProductionSupplierClaims.AnyAsync(x => x.ProductionOrderId == order.Id && x.Status == ProductionClaimStatus.Open, cancellationToken))
                return Result.Failure("Contractor recovery claims must be settled or cancelled before closure.");

            order.Status = ProductionOrderStatus.Closed;
            order.ClosedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success($"Production order {order.OrderNumber} closed and reconciled.");
        }

        public async Task<Result> Handle(SettleProductionClaimCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SettlementReference)) return Result.Failure("Supplier credit/debit-note reference is required.");
            var claim = await _context.ProductionSupplierClaims
                .FirstOrDefaultAsync(x => x.Id == request.ClaimId && x.ProductionOrderId == request.ProductionOrderId, cancellationToken);
            if (claim == null) return Result.Failure("Production supplier claim not found.");
            if (claim.Status != ProductionClaimStatus.Open) return Result.Failure("Only open claims can be settled.");

            // The AP debit was posted when the loss was classified; settlement records the supplier's confirming credit document without reposting GL.
            claim.Status = ProductionClaimStatus.Settled;
            claim.SettledDate = request.SettlementDate;
            claim.SettlementReference = request.SettlementReference.Trim();
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success($"Claim {claim.ClaimNumber} settled against {claim.SettlementReference}.");
        }
    }
}
