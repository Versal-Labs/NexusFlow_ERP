using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexusFlow.Domain.Entities.Inventory
{
    [Table("ProductionOrders", Schema = "Inventory")]
    public class ProductionOrder : AuditableEntity
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int ContractorId { get; set; }
        public Supplier Contractor { get; set; } = null!;
        public int FinishedGoodVariantId { get; set; }
        public ProductVariant FinishedGoodVariant { get; set; } = null!;
        public int BillOfMaterialId { get; set; }
        public BillOfMaterial BillOfMaterial { get; set; } = null!;
        public int BomRevisionNumber { get; set; }
        public int SourceWarehouseId { get; set; }
        public Warehouse SourceWarehouse { get; set; } = null!;
        public int DestinationWarehouseId { get; set; }
        public Warehouse DestinationWarehouse { get; set; } = null!;
        public decimal TargetQuantity { get; set; }
        public decimal OverproductionTolerancePercent { get; set; }
        public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Draft;
        public string Notes { get; set; } = string.Empty;
        public DateTime? ReleasedAtUtc { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
        public ICollection<ProductionOrderComponent> Components { get; set; } = new List<ProductionOrderComponent>();
        public ICollection<ProductionMaterialMovement> MaterialMovements { get; set; } = new List<ProductionMaterialMovement>();
        public ICollection<ProductionReceipt> Receipts { get; set; } = new List<ProductionReceipt>();
        public ICollection<ProductionOrderRevision> Revisions { get; set; } = new List<ProductionOrderRevision>();

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    [Table("ProductionOrderRevisions", Schema = "Inventory")]
    public class ProductionOrderRevision : AuditableEntity
    {
        public int ProductionOrderId { get; set; }
        public ProductionOrder ProductionOrder { get; set; } = null!;
        public int RevisionNumber { get; set; }
        public DateTime RevisionDate { get; set; }
        public decimal PreviousTargetQuantity { get; set; }
        public decimal NewTargetQuantity { get; set; }
        public decimal PreviousTolerancePercent { get; set; }
        public decimal NewTolerancePercent { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    [Table("ProductionOrderComponents", Schema = "Inventory")]
    public class ProductionOrderComponent : AuditableEntity
    {
        public int ProductionOrderId { get; set; }
        public ProductionOrder ProductionOrder { get; set; } = null!;
        public int MaterialVariantId { get; set; }
        public ProductVariant MaterialVariant { get; set; } = null!;
        public decimal QuantityPerUnit { get; set; }
        public decimal PlannedQuantity { get; set; }
        public decimal IssuedQuantity { get; set; }
        public decimal IssuedCost { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public decimal ReturnedCost { get; set; }
        public decimal ConsumedQuantity { get; set; }
        public decimal ConsumedCost { get; set; }
        public decimal NormalWasteQuantity { get; set; }
        public decimal NormalWasteCost { get; set; }
        public decimal AbnormalLossQuantity { get; set; }
        public decimal AbnormalLossCost { get; set; }
        public decimal ContractorRecoverableQuantity { get; set; }
        public decimal ContractorRecoverableCost { get; set; }

        [NotMapped]
        public decimal ContractorHeldQuantity => IssuedQuantity - ReturnedQuantity - ConsumedQuantity - NormalWasteQuantity - AbnormalLossQuantity - ContractorRecoverableQuantity;

        [NotMapped]
        public decimal UnallocatedWipCost => IssuedCost - ReturnedCost - ConsumedCost - NormalWasteCost - AbnormalLossCost - ContractorRecoverableCost;
    }

    [Table("ProductionMaterialMovements", Schema = "Inventory")]
    public class ProductionMaterialMovement : AuditableEntity
    {
        public int ProductionOrderId { get; set; }
        public ProductionOrder ProductionOrder { get; set; } = null!;
        public string ReferenceNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public ProductionMaterialMovementType Type { get; set; }
        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = null!;
        public string Notes { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public ICollection<ProductionMaterialMovementLine> Lines { get; set; } = new List<ProductionMaterialMovementLine>();
    }

    [Table("ProductionMaterialMovementLines", Schema = "Inventory")]
    public class ProductionMaterialMovementLine : AuditableEntity
    {
        public int ProductionMaterialMovementId { get; set; }
        public ProductionMaterialMovement ProductionMaterialMovement { get; set; } = null!;
        public int ProductionOrderComponentId { get; set; }
        public ProductionOrderComponent ProductionOrderComponent { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
    }

    [Table("ProductionReceipts", Schema = "Inventory")]
    public class ProductionReceipt : AuditableEntity
    {
        public int ProductionOrderId { get; set; }
        public ProductionOrder ProductionOrder { get; set; } = null!;
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public decimal SewingCharge { get; set; }
        public decimal MaterialCostCapitalized { get; set; }
        public decimal NormalWasteCost { get; set; }
        public decimal AbnormalLossCost { get; set; }
        public decimal ContractorRecoverableCost { get; set; }
        public decimal FinishedGoodsCost { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int? SupplierBillId { get; set; }
        public SupplierBill? SupplierBill { get; set; }
        public ICollection<ProductionReceiptConsumption> Consumptions { get; set; } = new List<ProductionReceiptConsumption>();
    }

    [Table("ProductionReceiptConsumptions", Schema = "Inventory")]
    public class ProductionReceiptConsumption : AuditableEntity
    {
        public int ProductionReceiptId { get; set; }
        public ProductionReceipt ProductionReceipt { get; set; } = null!;
        public int ProductionOrderComponentId { get; set; }
        public ProductionOrderComponent ProductionOrderComponent { get; set; } = null!;
        public decimal ConsumedQuantity { get; set; }
        public decimal ConsumedCost { get; set; }
        public decimal NormalWasteQuantity { get; set; }
        public decimal NormalWasteCost { get; set; }
        public decimal AbnormalLossQuantity { get; set; }
        public decimal AbnormalLossCost { get; set; }
        public decimal ContractorRecoverableQuantity { get; set; }
        public decimal ContractorRecoverableCost { get; set; }
    }

    [Table("ProductionSupplierClaims", Schema = "Inventory")]
    public class ProductionSupplierClaim : AuditableEntity
    {
        public int ProductionOrderId { get; set; }
        public ProductionOrder ProductionOrder { get; set; } = null!;
        public int ProductionReceiptId { get; set; }
        public ProductionReceipt ProductionReceipt { get; set; } = null!;
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;
        public string ClaimNumber { get; set; } = string.Empty;
        public DateTime ClaimDate { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public ProductionClaimStatus Status { get; set; } = ProductionClaimStatus.Open;
        public DateTime? SettledDate { get; set; }
        public string SettlementReference { get; set; } = string.Empty;
    }
}
