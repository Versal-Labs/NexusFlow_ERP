using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Inventory.ProductionOrders
{
    public record GetProductionOrdersQuery : IRequest<Result<List<ProductionOrderListDto>>>;
    public record GetProductionOrderDetailQuery(int Id) : IRequest<Result<ProductionOrderDetailDto>>;
    public record GetUnbilledProductionReceiptsQuery(int SupplierId) : IRequest<Result<List<UnbilledProductionReceiptDto>>>;

    public class UnbilledProductionReceiptDto
    {
        public int Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public decimal SewingCharge { get; set; }
    }

    public class ProductionOrderListDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string ContractorName { get; set; } = string.Empty;
        public string FinishedGood { get; set; } = string.Empty;
        public decimal TargetQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ProductionOrderDetailDto : ProductionOrderListDto
    {
        public int ContractorId { get; set; }
        public int FinishedGoodVariantId { get; set; }
        public int BillOfMaterialId { get; set; }
        public int BomRevisionNumber { get; set; }
        public int SourceWarehouseId { get; set; }
        public string SourceWarehouse { get; set; } = string.Empty;
        public int DestinationWarehouseId { get; set; }
        public string DestinationWarehouse { get; set; } = string.Empty;
        public decimal TolerancePercent { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string RowVersion { get; set; } = string.Empty;
        public decimal RejectedQuantity { get; set; }
        public decimal SewingAccrued { get; set; }
        public decimal SewingBilled { get; set; }
        public decimal OpenClaimAmount { get; set; }
        public bool CanClose { get; set; }
        public List<ProductionComponentDto> Components { get; set; } = new();
        public List<ProductionMovementDto> Movements { get; set; } = new();
        public List<ProductionReceiptDto> Receipts { get; set; } = new();
        public List<ProductionRevisionDto> Revisions { get; set; } = new();
        public List<ProductionClaimDto> Claims { get; set; } = new();
    }

    public class ProductionComponentDto
    {
        public int Id { get; set; }
        public int MaterialVariantId { get; set; }
        public string Material { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal QuantityPerUnit { get; set; }
        public decimal Planned { get; set; }
        public decimal Issued { get; set; }
        public decimal Returned { get; set; }
        public decimal Consumed { get; set; }
        public decimal NormalWaste { get; set; }
        public decimal AbnormalLoss { get; set; }
        public decimal ContractorRecoverable { get; set; }
        public decimal ContractorHeld { get; set; }
        public decimal UnallocatedWipCost { get; set; }
    }

    public class ProductionMovementDto
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class ProductionReceiptDto
    {
        public int Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public decimal SewingCharge { get; set; }
        public decimal FinishedGoodsCost { get; set; }
        public decimal AbnormalLossCost { get; set; }
        public decimal ContractorRecoverableCost { get; set; }
        public bool IsSewingBilled { get; set; }
    }

    public class ProductionRevisionDto
    {
        public int RevisionNumber { get; set; }
        public DateTime RevisionDate { get; set; }
        public decimal PreviousTargetQuantity { get; set; }
        public decimal NewTargetQuantity { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ProductionClaimDto
    {
        public int Id { get; set; }
        public string ClaimNumber { get; set; } = string.Empty;
        public DateTime ClaimDate { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SettlementReference { get; set; } = string.Empty;
    }

    public class ProductionOrderQueryHandler :
        IRequestHandler<GetProductionOrdersQuery, Result<List<ProductionOrderListDto>>>,
        IRequestHandler<GetProductionOrderDetailQuery, Result<ProductionOrderDetailDto>>,
        IRequestHandler<GetUnbilledProductionReceiptsQuery, Result<List<UnbilledProductionReceiptDto>>>
    {
        private readonly IErpDbContext _context;
        public ProductionOrderQueryHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<ProductionOrderListDto>>> Handle(GetProductionOrdersQuery request, CancellationToken cancellationToken)
        {
            var rows = await _context.ProductionOrders.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(x => new ProductionOrderListDto
                {
                    Id = x.Id, OrderNumber = x.OrderNumber, OrderDate = x.OrderDate,
                    ContractorName = x.Contractor.Name,
                    FinishedGood = x.FinishedGoodVariant.Product.Name + " - " + x.FinishedGoodVariant.SKU,
                    TargetQuantity = x.TargetQuantity,
                    AcceptedQuantity = x.Receipts.Sum(r => r.AcceptedQuantity),
                    Status = x.Status.ToString()
                }).ToListAsync(cancellationToken);
            return Result<List<ProductionOrderListDto>>.Success(rows);
        }

        public async Task<Result<ProductionOrderDetailDto>> Handle(GetProductionOrderDetailQuery request, CancellationToken cancellationToken)
        {
            var order = await _context.ProductionOrders.AsNoTracking()
                .Include(x => x.Contractor)
                .Include(x => x.FinishedGoodVariant).ThenInclude(x => x.Product)
                .Include(x => x.SourceWarehouse).Include(x => x.DestinationWarehouse)
                .Include(x => x.Components).ThenInclude(x => x.MaterialVariant).ThenInclude(x => x.Product)
                .Include(x => x.MaterialMovements)
                .Include(x => x.Receipts)
                .Include(x => x.Revisions)
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (order == null) return Result<ProductionOrderDetailDto>.Failure("Production order not found.");

            var claims = await _context.ProductionSupplierClaims.AsNoTracking()
                .Where(x => x.ProductionOrderId == order.Id).OrderByDescending(x => x.Id).ToListAsync(cancellationToken);
            var openClaimAmount = claims.Where(x => x.Status == ProductionClaimStatus.Open).Sum(x => x.Amount);
            var sewingAccrued = order.Receipts.Sum(x => x.SewingCharge);
            var sewingBilled = order.Receipts.Where(x => x.SupplierBillId.HasValue).Sum(x => x.SewingCharge);
            var materialReconciled = order.Components.All(x => Math.Abs(x.ContractorHeldQuantity) < 0.0001m && Math.Abs(x.UnallocatedWipCost) < 0.01m);

            var dto = new ProductionOrderDetailDto
            {
                Id = order.Id, OrderNumber = order.OrderNumber, OrderDate = order.OrderDate,
                ContractorId = order.ContractorId, ContractorName = order.Contractor.Name,
                FinishedGoodVariantId = order.FinishedGoodVariantId,
                FinishedGood = order.FinishedGoodVariant.Product.Name + " - " + order.FinishedGoodVariant.SKU,
                BillOfMaterialId = order.BillOfMaterialId, BomRevisionNumber = order.BomRevisionNumber,
                SourceWarehouseId = order.SourceWarehouseId, SourceWarehouse = order.SourceWarehouse.Name,
                DestinationWarehouseId = order.DestinationWarehouseId, DestinationWarehouse = order.DestinationWarehouse.Name,
                TargetQuantity = order.TargetQuantity, TolerancePercent = order.OverproductionTolerancePercent,
                PlannedStartDate = order.PlannedStartDate, DueDate = order.DueDate, Notes = order.Notes,
                Status = order.Status.ToString(), RowVersion = Convert.ToBase64String(order.RowVersion),
                AcceptedQuantity = order.Receipts.Sum(x => x.AcceptedQuantity),
                RejectedQuantity = order.Receipts.Sum(x => x.RejectedQuantity),
                SewingAccrued = sewingAccrued, SewingBilled = sewingBilled, OpenClaimAmount = openClaimAmount,
                CanClose = order.Status == ProductionOrderStatus.ReadyToClose && materialReconciled && sewingAccrued == sewingBilled && openClaimAmount == 0,
                Components = order.Components.Select(x => new ProductionComponentDto
                {
                    Id = x.Id, MaterialVariantId = x.MaterialVariantId,
                    Material = x.MaterialVariant.Product.Name, Sku = x.MaterialVariant.SKU,
                    QuantityPerUnit = x.QuantityPerUnit, Planned = x.PlannedQuantity,
                    Issued = x.IssuedQuantity, Returned = x.ReturnedQuantity, Consumed = x.ConsumedQuantity,
                    NormalWaste = x.NormalWasteQuantity, AbnormalLoss = x.AbnormalLossQuantity,
                    ContractorRecoverable = x.ContractorRecoverableQuantity,
                    ContractorHeld = x.ContractorHeldQuantity, UnallocatedWipCost = x.UnallocatedWipCost
                }).ToList(),
                Movements = order.MaterialMovements.OrderByDescending(x => x.Date).Select(x => new ProductionMovementDto
                {
                    Id = x.Id, ReferenceNumber = x.ReferenceNumber, Date = x.Date,
                    Type = x.Type.ToString(), TotalCost = x.TotalCost, Notes = x.Notes
                }).ToList(),
                Receipts = order.Receipts.OrderByDescending(x => x.ReceiptDate).Select(x => new ProductionReceiptDto
                {
                    Id = x.Id, ReceiptNumber = x.ReceiptNumber, ReceiptDate = x.ReceiptDate,
                    AcceptedQuantity = x.AcceptedQuantity, RejectedQuantity = x.RejectedQuantity,
                    SewingCharge = x.SewingCharge, FinishedGoodsCost = x.FinishedGoodsCost,
                    AbnormalLossCost = x.AbnormalLossCost, ContractorRecoverableCost = x.ContractorRecoverableCost,
                    IsSewingBilled = x.SupplierBillId.HasValue
                }).ToList(),
                Revisions = order.Revisions.OrderByDescending(x => x.RevisionNumber).Select(x => new ProductionRevisionDto
                {
                    RevisionNumber = x.RevisionNumber, RevisionDate = x.RevisionDate,
                    PreviousTargetQuantity = x.PreviousTargetQuantity, NewTargetQuantity = x.NewTargetQuantity, Reason = x.Reason
                }).ToList(),
                Claims = claims.Select(x => new ProductionClaimDto
                {
                    Id = x.Id, ClaimNumber = x.ClaimNumber, ClaimDate = x.ClaimDate, Amount = x.Amount,
                    Reason = x.Reason, Status = x.Status.ToString(), SettlementReference = x.SettlementReference
                }).ToList()
            };
            return Result<ProductionOrderDetailDto>.Success(dto);
        }

        public async Task<Result<List<UnbilledProductionReceiptDto>>> Handle(GetUnbilledProductionReceiptsQuery request, CancellationToken cancellationToken)
        {
            var rows = await _context.ProductionReceipts.AsNoTracking()
                .Where(x => x.ProductionOrder.ContractorId == request.SupplierId && x.SewingCharge > 0 && !x.SupplierBillId.HasValue)
                .OrderBy(x => x.ReceiptDate)
                .Select(x => new UnbilledProductionReceiptDto
                {
                    Id = x.Id, ReceiptNumber = x.ReceiptNumber, OrderNumber = x.ProductionOrder.OrderNumber,
                    ReceiptDate = x.ReceiptDate, SewingCharge = x.SewingCharge
                }).ToListAsync(cancellationToken);
            return Result<List<UnbilledProductionReceiptDto>>.Success(rows);
        }
    }
}
