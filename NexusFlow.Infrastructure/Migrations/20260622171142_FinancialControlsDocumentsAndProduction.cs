using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialControlsDocumentsAndProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReversalReferenceNo",
                schema: "Finance",
                table: "PaymentTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                schema: "Finance",
                table: "PaymentTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                schema: "Finance",
                table: "PaymentTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClearedDate",
                schema: "Finance",
                table: "ChequeRegister",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DishonoredDate",
                schema: "Finance",
                table: "ChequeRegister",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndorsedDate",
                schema: "Finance",
                table: "ChequeRegister",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReferenceNo",
                schema: "Finance",
                table: "ChequeRegister",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                schema: "Master",
                table: "BillOfMaterials",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BasisQuantity",
                schema: "Master",
                table: "BillOfMaterials",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                schema: "Master",
                table: "BillOfMaterials",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveTo",
                schema: "Master",
                table: "BillOfMaterials",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                schema: "Master",
                table: "BillOfMaterials",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                schema: "Master",
                table: "BillOfMaterials",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "Master",
                table: "BillOfMaterials",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            // Existing recipes become approved revision 1; this preserves today's behavior while enabling immutable future revisions.
            migrationBuilder.Sql(@"
                UPDATE [Master].[BillOfMaterials]
                SET [RevisionNumber] = 1,
                    [BasisQuantity] = 1,
                    [EffectiveFrom] = CAST(SYSUTCDATETIME() AS date),
                    [IsApproved] = 1,
                    [ApprovedAtUtc] = SYSUTCDATETIME()
                WHERE [RevisionNumber] = 0 OR [BasisQuantity] = 0;");

            // Repair only missing sequence modules. Existing prefixes and counters are deliberately untouched.
            migrationBuilder.Sql(@"
                DECLARE @Sequences TABLE ([Module] nvarchar(50), [Prefix] nvarchar(20));
                INSERT INTO @Sequences ([Module], [Prefix]) VALUES
                    ('CreditNote','CN'),('DebitNote','DN'),('EMP','EMP'),('GRN','GRN'),('JOURNAL','JE'),
                    ('MaterialIssue','MI'),('OpeningStock','OBSTK'),('ORD','SO'),('Payment','PAY'),
                    ('ProductionReceipt','PRD'),('Purchasing','PO'),('Receipt','REC'),('SalesInvoice','INV'),
                    ('StockAdjustment','ADJ'),('StockTake','ST'),('StockTransfer','TRF'),('SupplierBill','BILL'),
                    ('CustomerDebitMemo','CDM'),('ProductionOrder','PWO'),('MaterialReturn','MR');

                INSERT INTO [Config].[NumberSequences]
                    ([Module],[Prefix],[NextNumber],[Delimiter],[Suffix],[LastUsed],[CreatedAt],[CreatedBy])
                SELECT s.[Module], s.[Prefix], 1, '-', '', SYSUTCDATETIME(), SYSUTCDATETIME(), 'MIGRATION'
                FROM @Sequences s
                WHERE NOT EXISTS (SELECT 1 FROM [Config].[NumberSequences] n WHERE n.[Module] = s.[Module]);

                IF NOT EXISTS (SELECT 1 FROM [Config].[SystemConfigs] WHERE [Key] = 'Production.OverproductionTolerancePercent')
                    INSERT INTO [Config].[SystemConfigs] ([Key],[Value],[DataType],[Description],[CreatedAt],[CreatedBy])
                    VALUES ('Production.OverproductionTolerancePercent','5','Decimal','Maximum accepted output above a production order target without revision.',SYSUTCDATETIME(),'MIGRATION');

                UPDATE p SET p.[Method] = 5
                FROM [Finance].[PaymentTransactions] p
                INNER JOIN [Finance].[ChequeRegister] c ON c.[EndorsedPaymentId] = p.[Id]
                WHERE p.[Method] = 3;");

            migrationBuilder.CreateTable(
                name: "CustomerDebitMemos",
                schema: "Sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DebitMemoNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ChequeRegisterId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDebitMemos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerDebitMemos_ChequeRegister_ChequeRegisterId",
                        column: x => x.ChequeRegisterId,
                        principalSchema: "Finance",
                        principalTable: "ChequeRegister",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CustomerDebitMemos_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "Sales",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "CustomerDebitMemos_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Sales")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "GeneratedDocuments",
                schema: "System",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputAction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlobUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sha256Hash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverrideDifferencesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedDocuments", x => x.Id);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GeneratedDocuments_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "System")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "ProductionOrders",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlannedStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractorId = table.Column<int>(type: "int", nullable: false),
                    FinishedGoodVariantId = table.Column<int>(type: "int", nullable: false),
                    BillOfMaterialId = table.Column<int>(type: "int", nullable: false),
                    BomRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    SourceWarehouseId = table.Column<int>(type: "int", nullable: false),
                    DestinationWarehouseId = table.Column<int>(type: "int", nullable: false),
                    TargetQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OverproductionTolerancePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_BillOfMaterials_BillOfMaterialId",
                        column: x => x.BillOfMaterialId,
                        principalSchema: "Master",
                        principalTable: "BillOfMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_ProductVariants_FinishedGoodVariantId",
                        column: x => x.FinishedGoodVariantId,
                        principalSchema: "Master",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_Suppliers_ContractorId",
                        column: x => x.ContractorId,
                        principalSchema: "Purchasing",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalSchema: "Master",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_Warehouses_SourceWarehouseId",
                        column: x => x.SourceWarehouseId,
                        principalSchema: "Master",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionOrders_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "ProductionMaterialMovements",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionMaterialMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialMovements_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalSchema: "Inventory",
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialMovements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "Master",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionMaterialMovements_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "ProductionOrderComponents",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    MaterialVariantId = table.Column<int>(type: "int", nullable: false),
                    QuantityPerUnit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IssuedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IssuedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReturnedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ConsumedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NormalWasteQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NormalWasteCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AbnormalLossQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AbnormalLossCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ContractorRecoverableQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ContractorRecoverableCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrderComponents_ProductVariants_MaterialVariantId",
                        column: x => x.MaterialVariantId,
                        principalSchema: "Master",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrderComponents_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalSchema: "Inventory",
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionOrderComponents_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "ProductionOrderRevisions",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousTargetQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NewTargetQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PreviousTolerancePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewTolerancePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrderRevisions_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalSchema: "Inventory",
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionOrderRevisions_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "ProductionReceipts",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SewingCharge = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaterialCostCapitalized = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NormalWasteCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AbnormalLossCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ContractorRecoverableCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinishedGoodsCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplierBillId = table.Column<int>(type: "int", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionReceipts_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalSchema: "Inventory",
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionReceipts_SupplierBills_SupplierBillId",
                        column: x => x.SupplierBillId,
                        principalSchema: "Purchasing",
                        principalTable: "SupplierBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionReceipts_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "ProductionMaterialMovementLines",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionMaterialMovementId = table.Column<int>(type: "int", nullable: false),
                    ProductionOrderComponentId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionMaterialMovementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialMovementLines_ProductionMaterialMovements_ProductionMaterialMovementId",
                        column: x => x.ProductionMaterialMovementId,
                        principalSchema: "Inventory",
                        principalTable: "ProductionMaterialMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialMovementLines_ProductionOrderComponents_ProductionOrderComponentId",
                        column: x => x.ProductionOrderComponentId,
                        principalSchema: "Inventory",
                        principalTable: "ProductionOrderComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionMaterialMovementLines_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "ProductionReceiptConsumptions",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionReceiptId = table.Column<int>(type: "int", nullable: false),
                    ProductionOrderComponentId = table.Column<int>(type: "int", nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ConsumedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NormalWasteQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NormalWasteCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AbnormalLossQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AbnormalLossCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ContractorRecoverableQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ContractorRecoverableCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionReceiptConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionReceiptConsumptions_ProductionOrderComponents_ProductionOrderComponentId",
                        column: x => x.ProductionOrderComponentId,
                        principalSchema: "Inventory",
                        principalTable: "ProductionOrderComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionReceiptConsumptions_ProductionReceipts_ProductionReceiptId",
                        column: x => x.ProductionReceiptId,
                        principalSchema: "Inventory",
                        principalTable: "ProductionReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionReceiptConsumptions_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "ProductionSupplierClaims",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductionReceiptId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    ClaimNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClaimDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SettledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SettlementReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionSupplierClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionSupplierClaims_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalSchema: "Inventory",
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionSupplierClaims_ProductionReceipts_ProductionReceiptId",
                        column: x => x.ProductionReceiptId,
                        principalSchema: "Inventory",
                        principalTable: "ProductionReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionSupplierClaims_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "Purchasing",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionSupplierClaims_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDebitMemos_ChequeRegisterId",
                schema: "Sales",
                table: "CustomerDebitMemos",
                column: "ChequeRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDebitMemos_CustomerId",
                schema: "Sales",
                table: "CustomerDebitMemos",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialMovementLines_ProductionMaterialMovementId",
                schema: "Inventory",
                table: "ProductionMaterialMovementLines",
                column: "ProductionMaterialMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialMovementLines_ProductionOrderComponentId",
                schema: "Inventory",
                table: "ProductionMaterialMovementLines",
                column: "ProductionOrderComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialMovements_ProductionOrderId",
                schema: "Inventory",
                table: "ProductionMaterialMovements",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialMovements_WarehouseId",
                schema: "Inventory",
                table: "ProductionMaterialMovements",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderComponents_MaterialVariantId",
                schema: "Inventory",
                table: "ProductionOrderComponents",
                column: "MaterialVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderComponents_ProductionOrderId",
                schema: "Inventory",
                table: "ProductionOrderComponents",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderRevisions_ProductionOrderId",
                schema: "Inventory",
                table: "ProductionOrderRevisions",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_BillOfMaterialId",
                schema: "Inventory",
                table: "ProductionOrders",
                column: "BillOfMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_ContractorId",
                schema: "Inventory",
                table: "ProductionOrders",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_DestinationWarehouseId",
                schema: "Inventory",
                table: "ProductionOrders",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_FinishedGoodVariantId",
                schema: "Inventory",
                table: "ProductionOrders",
                column: "FinishedGoodVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_SourceWarehouseId",
                schema: "Inventory",
                table: "ProductionOrders",
                column: "SourceWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReceiptConsumptions_ProductionOrderComponentId",
                schema: "Inventory",
                table: "ProductionReceiptConsumptions",
                column: "ProductionOrderComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReceiptConsumptions_ProductionReceiptId",
                schema: "Inventory",
                table: "ProductionReceiptConsumptions",
                column: "ProductionReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReceipts_ProductionOrderId",
                schema: "Inventory",
                table: "ProductionReceipts",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReceipts_SupplierBillId",
                schema: "Inventory",
                table: "ProductionReceipts",
                column: "SupplierBillId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplierClaims_ProductionOrderId",
                schema: "Inventory",
                table: "ProductionSupplierClaims",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplierClaims_ProductionReceiptId",
                schema: "Inventory",
                table: "ProductionSupplierClaims",
                column: "ProductionReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplierClaims_SupplierId",
                schema: "Inventory",
                table: "ProductionSupplierClaims",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerDebitMemos",
                schema: "Sales")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "CustomerDebitMemos_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Sales")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "GeneratedDocuments",
                schema: "System")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GeneratedDocuments_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "System")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "ProductionMaterialMovementLines",
                schema: "Inventory")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionMaterialMovementLines_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "ProductionOrderRevisions",
                schema: "Inventory")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionOrderRevisions_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "ProductionReceiptConsumptions",
                schema: "Inventory")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionReceiptConsumptions_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "ProductionSupplierClaims",
                schema: "Inventory")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionSupplierClaims_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "ProductionMaterialMovements",
                schema: "Inventory")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionMaterialMovements_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "ProductionOrderComponents",
                schema: "Inventory")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionOrderComponents_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "ProductionReceipts",
                schema: "Inventory")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionReceipts_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "ProductionOrders",
                schema: "Inventory")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ProductionOrders_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Inventory")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropColumn(
                name: "ReversalReferenceNo",
                schema: "Finance",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                schema: "Finance",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                schema: "Finance",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ClearedDate",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "DishonoredDate",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "EndorsedDate",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "ReversalReferenceNo",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                schema: "Master",
                table: "BillOfMaterials");

            migrationBuilder.DropColumn(
                name: "BasisQuantity",
                schema: "Master",
                table: "BillOfMaterials");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                schema: "Master",
                table: "BillOfMaterials");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                schema: "Master",
                table: "BillOfMaterials");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                schema: "Master",
                table: "BillOfMaterials");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                schema: "Master",
                table: "BillOfMaterials");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "Master",
                table: "BillOfMaterials");
        }
    }
}
