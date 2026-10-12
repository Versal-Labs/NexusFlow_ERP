using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationCentre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TaxRegNo",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocation_Target",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_ChequeRegister_MigrationBatchId_LegacyReceiptReference",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.EnsureSchema(
                name: "Migration");

            migrationBuilder.AddColumn<bool>(
                name: "DetailsIncomplete",
                schema: "Purchasing",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegacySupplierCode",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Inventory",
                table: "StockTransactions",
                type: "decimal(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Inventory",
                table: "StockTakeItems",
                type: "decimal(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Inventory",
                table: "StockLayers",
                type: "decimal(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Purchasing",
                table: "PurchaseOrderItems",
                type: "decimal(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "MovingAverageCost",
                schema: "Master",
                table: "ProductVariants",
                type: "decimal(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CostPrice",
                schema: "Master",
                table: "ProductVariants",
                type: "decimal(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Inventory",
                table: "ProductionMaterialMovementLines",
                type: "decimal(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<int>(
                name: "SupplierOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Purchasing",
                table: "GRNItems",
                type: "decimal(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<bool>(
                name: "DetailsIncomplete",
                schema: "Sales",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OpeningEndorsedSupplierId",
                schema: "Finance",
                table: "ChequeRegister",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MigrationProjects",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OpeningDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ApprovedHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalEvidence = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MigrationProjects", x => x.Id);
                    table.CheckConstraint("CK_Migration_Dates", "[OpeningDate] = DATEADD(day, 1, [ClosingDate]) AND [Currency] = 'LKR'");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationProjects_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "MigrationAudit",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_MigrationAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationAudit_MigrationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "Migration",
                        principalTable: "MigrationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationAudit_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "MigrationDatasets",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClosingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_MigrationDatasets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationDatasets_MigrationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "Migration",
                        principalTable: "MigrationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationDatasets_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "MigrationRuns",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ApprovedHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SelectionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CommittedUnits = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_MigrationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationRuns_MigrationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "Migration",
                        principalTable: "MigrationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationRuns_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "MigrationScopes",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    RequiredModulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_MigrationScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationScopes_MigrationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "Migration",
                        principalTable: "MigrationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationScopes_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "MigrationUnits",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ScopeKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    JournalEntryId = table.Column<int>(type: "int", nullable: true),
                    ReversalJournalEntryId = table.Column<int>(type: "int", nullable: true),
                    ReversedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_MigrationUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationUnits_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "Finance",
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationUnits_JournalEntries_ReversalJournalEntryId",
                        column: x => x.ReversalJournalEntryId,
                        principalSchema: "Finance",
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationUnits_MigrationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "Migration",
                        principalTable: "MigrationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationUnits_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "MigrationOrigins",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RecordKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IdentityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LegacyRecordId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EntityKind = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MigrationOrigins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationOrigins_MigrationUnits_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "Migration",
                        principalTable: "MigrationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationOrigins_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "OpeningBankItems",
                schema: "Finance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChequeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChequeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedChequeId = table.Column<int>(type: "int", nullable: true),
                    MigrationUnitId = table.Column<int>(type: "int", nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LedgerAlreadyReflected = table.Column<bool>(type: "bit", nullable: false),
                    ClearedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionJournalId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_OpeningBankItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningBankItems_Accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "Finance",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBankItems_ChequeRegister_ReceivedChequeId",
                        column: x => x.ReceivedChequeId,
                        principalSchema: "Finance",
                        principalTable: "ChequeRegister",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBankItems_MigrationUnits_MigrationUnitId",
                        column: x => x.MigrationUnitId,
                        principalSchema: "Migration",
                        principalTable: "MigrationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBankItems_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "Purchasing",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "OpeningBankItems_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Finance")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "SupplierOpeningItems",
                schema: "Purchasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationUnitId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayableAccountId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_SupplierOpeningItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierOpeningItems_Accounts_PayableAccountId",
                        column: x => x.PayableAccountId,
                        principalSchema: "Finance",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierOpeningItems_MigrationUnits_MigrationUnitId",
                        column: x => x.MigrationUnitId,
                        principalSchema: "Migration",
                        principalTable: "MigrationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierOpeningItems_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "Purchasing",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SupplierOpeningItems_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Purchasing")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "SupplierCreditApplications",
                schema: "Finance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditOpeningItemId = table.Column<int>(type: "int", nullable: false),
                    SupplierOpeningItemId = table.Column<int>(type: "int", nullable: true),
                    SupplierBillId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_SupplierCreditApplications", x => x.Id);
                    table.CheckConstraint("CK_SupplierCredit_Target", "[Amount] > 0 AND (([SupplierOpeningItemId] IS NULL AND [SupplierBillId] IS NOT NULL) OR ([SupplierOpeningItemId] IS NOT NULL AND [SupplierBillId] IS NULL))");
                    table.ForeignKey(
                        name: "FK_SupplierCreditApplications_SupplierBills_SupplierBillId",
                        column: x => x.SupplierBillId,
                        principalSchema: "Purchasing",
                        principalTable: "SupplierBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCreditApplications_SupplierOpeningItems_CreditOpeningItemId",
                        column: x => x.CreditOpeningItemId,
                        principalSchema: "Purchasing",
                        principalTable: "SupplierOpeningItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCreditApplications_SupplierOpeningItems_SupplierOpeningItemId",
                        column: x => x.SupplierOpeningItemId,
                        principalSchema: "Purchasing",
                        principalTable: "SupplierOpeningItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SupplierCreditApplications_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Finance")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            // Delay name binding until the preceding ADD COLUMN in combined upgrade scripts.
            migrationBuilder.Sql("EXEC(N'CREATE UNIQUE INDEX [IX_Suppliers_LegacySupplierCode] ON [Purchasing].[Suppliers] ([LegacySupplierCode]) WHERE [LegacySupplierCode] IS NOT NULL');");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TaxRegNo",
                schema: "Purchasing",
                table: "Suppliers",
                column: "TaxRegNo",
                unique: true,
                filter: "[TaxRegNo] <> N''");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_SupplierOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations",
                column: "SupplierOpeningItemId");

            migrationBuilder.Sql("EXEC(N'ALTER TABLE [Finance].[PaymentAllocations] ADD CONSTRAINT [CK_PaymentAllocation_Target] CHECK ((CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierBillId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierOpeningItemId IS NULL THEN 0 ELSE 1 END) = 1 AND AmountAllocated > 0)');");

            migrationBuilder.CreateIndex(
                name: "IX_ChequeRegister_MigrationBatchId_LegacyReceiptReference",
                schema: "Finance",
                table: "ChequeRegister",
                columns: new[] { "MigrationBatchId", "LegacyReceiptReference" },
                filter: "[MigrationBatchId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChequeRegister_OpeningEndorsedSupplierId",
                schema: "Finance",
                table: "ChequeRegister",
                column: "OpeningEndorsedSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationAudit_ProjectId",
                schema: "Migration",
                table: "MigrationAudit",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationDatasets_ProjectId_Module",
                schema: "Migration",
                table: "MigrationDatasets",
                columns: new[] { "ProjectId", "Module" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationDatasets_ProjectId_Module_Revision",
                schema: "Migration",
                table: "MigrationDatasets",
                columns: new[] { "ProjectId", "Module", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOrigins_SourceSystem_RecordKind_IdentityHash",
                schema: "Migration",
                table: "MigrationOrigins",
                columns: new[] { "SourceSystem", "RecordKind", "IdentityHash" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOrigins_UnitId",
                schema: "Migration",
                table: "MigrationOrigins",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationProjects_Status",
                schema: "Migration",
                table: "MigrationProjects",
                column: "Status",
                unique: true,
                filter: "[Status] <> 'Finalized'");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRuns_ProjectId",
                schema: "Migration",
                table: "MigrationRuns",
                column: "ProjectId",
                unique: true,
                filter: "[Status] IN ('Queued','Running')");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationScopes_ProjectId_Kind_Code",
                schema: "Migration",
                table: "MigrationScopes",
                columns: new[] { "ProjectId", "Kind", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationUnits_JournalEntryId",
                schema: "Migration",
                table: "MigrationUnits",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationUnits_ProjectId_Module_ScopeKey",
                schema: "Migration",
                table: "MigrationUnits",
                columns: new[] { "ProjectId", "Module", "ScopeKey" },
                unique: true,
                filter: "[Status] = 'Posted'");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationUnits_ReversalJournalEntryId",
                schema: "Migration",
                table: "MigrationUnits",
                column: "ReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBankItems_BankAccountId",
                schema: "Finance",
                table: "OpeningBankItems",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBankItems_MigrationUnitId",
                schema: "Finance",
                table: "OpeningBankItems",
                column: "MigrationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBankItems_ReceivedChequeId",
                schema: "Finance",
                table: "OpeningBankItems",
                column: "ReceivedChequeId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBankItems_SupplierId",
                schema: "Finance",
                table: "OpeningBankItems",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditApplications_CreditOpeningItemId",
                schema: "Finance",
                table: "SupplierCreditApplications",
                column: "CreditOpeningItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditApplications_SupplierBillId",
                schema: "Finance",
                table: "SupplierCreditApplications",
                column: "SupplierBillId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditApplications_SupplierOpeningItemId",
                schema: "Finance",
                table: "SupplierCreditApplications",
                column: "SupplierOpeningItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOpeningItems_MigrationUnitId",
                schema: "Purchasing",
                table: "SupplierOpeningItems",
                column: "MigrationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOpeningItems_PayableAccountId",
                schema: "Purchasing",
                table: "SupplierOpeningItems",
                column: "PayableAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOpeningItems_SupplierId",
                schema: "Purchasing",
                table: "SupplierOpeningItems",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChequeRegister_Suppliers_OpeningEndorsedSupplierId",
                schema: "Finance",
                table: "ChequeRegister",
                column: "OpeningEndorsedSupplierId",
                principalSchema: "Purchasing",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocations_SupplierOpeningItems_SupplierOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations",
                column: "SupplierOpeningItemId",
                principalSchema: "Purchasing",
                principalTable: "SupplierOpeningItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChequeRegister_Suppliers_OpeningEndorsedSupplierId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocations_SupplierOpeningItems_SupplierOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "MigrationAudit",
                schema: "Migration")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationAudit_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "MigrationDatasets",
                schema: "Migration")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationDatasets_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "MigrationOrigins",
                schema: "Migration")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationOrigins_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "MigrationRuns",
                schema: "Migration")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationRuns_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "MigrationScopes",
                schema: "Migration")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationScopes_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "OpeningBankItems",
                schema: "Finance")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "OpeningBankItems_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Finance")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "SupplierCreditApplications",
                schema: "Finance")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SupplierCreditApplications_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Finance")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "SupplierOpeningItems",
                schema: "Purchasing")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SupplierOpeningItems_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Purchasing")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "MigrationUnits",
                schema: "Migration")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationUnits_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "MigrationProjects",
                schema: "Migration")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MigrationProjects_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Migration")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_LegacySupplierCode",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TaxRegNo",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAllocations_SupplierOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocation_Target",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_ChequeRegister_MigrationBatchId_LegacyReceiptReference",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropIndex(
                name: "IX_ChequeRegister_OpeningEndorsedSupplierId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "DetailsIncomplete",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "LegacySupplierCode",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "SupplierOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropColumn(
                name: "DetailsIncomplete",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "OpeningEndorsedSupplierId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Inventory",
                table: "StockTransactions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Inventory",
                table: "StockTakeItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Inventory",
                table: "StockLayers",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Purchasing",
                table: "PurchaseOrderItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "MovingAverageCost",
                schema: "Master",
                table: "ProductVariants",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CostPrice",
                schema: "Master",
                table: "ProductVariants",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Inventory",
                table: "ProductionMaterialMovementLines",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "Purchasing",
                table: "GRNItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(22,6)");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TaxRegNo",
                schema: "Purchasing",
                table: "Suppliers",
                column: "TaxRegNo",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAllocation_Target",
                schema: "Finance",
                table: "PaymentAllocations",
                sql: "(CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierBillId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END) = 1 AND AmountAllocated > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ChequeRegister_MigrationBatchId_LegacyReceiptReference",
                schema: "Finance",
                table: "ChequeRegister",
                columns: new[] { "MigrationBatchId", "LegacyReceiptReference" },
                unique: true,
                filter: "[MigrationBatchId] IS NOT NULL");
        }
    }
}
