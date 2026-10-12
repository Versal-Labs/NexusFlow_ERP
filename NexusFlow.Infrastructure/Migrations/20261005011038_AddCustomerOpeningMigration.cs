using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerOpeningMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChequeRegister_PaymentTransactions_OriginalReceiptId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidDate",
                schema: "Finance",
                table: "PaymentTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerDebitMemoId",
                schema: "Finance",
                table: "PaymentAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyCustomerCode",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "Latin1_General_100_CI_AS");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                schema: "Sales",
                table: "CustomerDebitMemos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivableAccountId",
                schema: "Sales",
                table: "CustomerDebitMemos",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OriginalReceiptId",
                schema: "Finance",
                table: "ChequeRegister",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ChequeRegister_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Finance")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.AddColumn<int>(
                name: "DepositJournalEntryId",
                schema: "Finance",
                table: "ChequeRegister",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepositedDate",
                schema: "Finance",
                table: "ChequeRegister",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyDocumentReferences",
                schema: "Finance",
                table: "ChequeRegister",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyReceiptReference",
                schema: "Finance",
                table: "ChequeRegister",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MigrationBatchId",
                schema: "Finance",
                table: "ChequeRegister",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpeningReceivableAccountId",
                schema: "Finance",
                table: "ChequeRegister",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpeningSourceRow",
                schema: "Finance",
                table: "ChequeRegister",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedDate",
                schema: "Finance",
                table: "ChequeRegister",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerMigrationBatches",
                schema: "Finance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CutoverDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceAsOfDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReviewHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OutstandingFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ChequeFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    OutstandingFile = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ChequeFile = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SourceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JournalEntryId = table.Column<int>(type: "int", nullable: true),
                    ReversalJournalEntryId = table.Column<int>(type: "int", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSignedOff = table.Column<bool>(type: "bit", nullable: false),
                    SignedOffAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedOffBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_CustomerMigrationBatches", x => x.Id);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "CustomerMigrationBatches_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Finance")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "CustomerOpeningItems",
                schema: "Sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationBatchId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ReceivableAccountId = table.Column<int>(type: "int", nullable: false),
                    LegacyCustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SourceDebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountSettled = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SourceRow = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_CustomerOpeningItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerOpeningItems_CustomerMigrationBatches_MigrationBatchId",
                        column: x => x.MigrationBatchId,
                        principalSchema: "Finance",
                        principalTable: "CustomerMigrationBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerOpeningItems_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "Sales",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "CustomerOpeningItems_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Sales")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "CustomerCreditApplications",
                schema: "Finance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditOpeningItemId = table.Column<int>(type: "int", nullable: false),
                    SalesInvoiceId = table.Column<int>(type: "int", nullable: true),
                    CustomerOpeningItemId = table.Column<int>(type: "int", nullable: true),
                    CustomerDebitMemoId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReversedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_CustomerCreditApplications", x => x.Id);
                    table.CheckConstraint("CK_CustomerCreditApplication_Target", "(CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END) = 1 AND Amount > 0");
                    table.ForeignKey(
                        name: "FK_CustomerCreditApplications_CustomerDebitMemos_CustomerDebitMemoId",
                        column: x => x.CustomerDebitMemoId,
                        principalSchema: "Sales",
                        principalTable: "CustomerDebitMemos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerCreditApplications_CustomerOpeningItems_CreditOpeningItemId",
                        column: x => x.CreditOpeningItemId,
                        principalSchema: "Sales",
                        principalTable: "CustomerOpeningItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerCreditApplications_CustomerOpeningItems_CustomerOpeningItemId",
                        column: x => x.CustomerOpeningItemId,
                        principalSchema: "Sales",
                        principalTable: "CustomerOpeningItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerCreditApplications_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalSchema: "Sales",
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "CustomerCreditApplications_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Finance")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_CustomerDebitMemoId",
                schema: "Finance",
                table: "PaymentAllocations",
                column: "CustomerDebitMemoId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_CustomerOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations",
                column: "CustomerOpeningItemId");

            // Dynamic SQL permits binding new filtered-index columns in a forward SQL script batch.
            migrationBuilder.Sql("EXEC(N'CREATE UNIQUE INDEX [IX_Customers_LegacyCustomerCode] ON [Sales].[Customers] ([LegacyCustomerCode]) WHERE [LegacyCustomerCode] IS NOT NULL;');");

            // Dynamic SQL permits binding new filtered-index columns in a forward SQL script batch.
            migrationBuilder.Sql("EXEC(N'CREATE UNIQUE INDEX [IX_ChequeRegister_MigrationBatchId_LegacyReceiptReference] ON [Finance].[ChequeRegister] ([MigrationBatchId], [LegacyReceiptReference]) WHERE [MigrationBatchId] IS NOT NULL;');");

            // Bind the new columns after ALTER TABLE has run in the script batch.
            migrationBuilder.Sql("EXEC(N'ALTER TABLE [Finance].[ChequeRegister] ADD CONSTRAINT [CK_Cheque_Origin] CHECK (([MigrationBatchId] IS NULL AND [OriginalReceiptId] IS NOT NULL) OR ([MigrationBatchId] IS NOT NULL AND [OriginalReceiptId] IS NULL AND [OpeningReceivableAccountId] IS NOT NULL));');");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCreditApplications_CreditOpeningItemId",
                schema: "Finance",
                table: "CustomerCreditApplications",
                column: "CreditOpeningItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCreditApplications_CustomerDebitMemoId",
                schema: "Finance",
                table: "CustomerCreditApplications",
                column: "CustomerDebitMemoId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCreditApplications_CustomerOpeningItemId",
                schema: "Finance",
                table: "CustomerCreditApplications",
                column: "CustomerOpeningItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCreditApplications_SalesInvoiceId",
                schema: "Finance",
                table: "CustomerCreditApplications",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMigrationBatches_SourceHash",
                schema: "Finance",
                table: "CustomerMigrationBatches",
                column: "SourceHash",
                unique: true,
                filter: "[Status] <> 'Reversed'");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOpeningItems_CustomerId",
                schema: "Sales",
                table: "CustomerOpeningItems",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOpeningItems_LegacyCustomerCode_ReferenceNo",
                schema: "Sales",
                table: "CustomerOpeningItems",
                columns: new[] { "LegacyCustomerCode", "ReferenceNo" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOpeningItems_MigrationBatchId_SourceRow",
                schema: "Sales",
                table: "CustomerOpeningItems",
                columns: new[] { "MigrationBatchId", "SourceRow" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChequeRegister_CustomerMigrationBatches_MigrationBatchId",
                schema: "Finance",
                table: "ChequeRegister",
                column: "MigrationBatchId",
                principalSchema: "Finance",
                principalTable: "CustomerMigrationBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChequeRegister_PaymentTransactions_OriginalReceiptId",
                schema: "Finance",
                table: "ChequeRegister",
                column: "OriginalReceiptId",
                principalSchema: "Finance",
                principalTable: "PaymentTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocations_CustomerDebitMemos_CustomerDebitMemoId",
                schema: "Finance",
                table: "PaymentAllocations",
                column: "CustomerDebitMemoId",
                principalSchema: "Sales",
                principalTable: "CustomerDebitMemos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocations_CustomerOpeningItems_CustomerOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations",
                column: "CustomerOpeningItemId",
                principalSchema: "Sales",
                principalTable: "CustomerOpeningItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChequeRegister_CustomerMigrationBatches_MigrationBatchId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropForeignKey(
                name: "FK_ChequeRegister_PaymentTransactions_OriginalReceiptId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocations_CustomerDebitMemos_CustomerDebitMemoId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocations_CustomerOpeningItems_CustomerOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "CustomerCreditApplications",
                schema: "Finance")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "CustomerCreditApplications_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Finance")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "CustomerOpeningItems",
                schema: "Sales")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "CustomerOpeningItems_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Sales")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "CustomerMigrationBatches",
                schema: "Finance")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "CustomerMigrationBatches_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Finance")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAllocations_CustomerDebitMemoId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAllocations_CustomerOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_Customers_LegacyCustomerCode",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_ChequeRegister_MigrationBatchId_LegacyReceiptReference",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Cheque_Origin",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "VoidDate",
                schema: "Finance",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "CustomerDebitMemoId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropColumn(
                name: "CustomerOpeningItemId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropColumn(
                name: "LegacyCustomerCode",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DueDate",
                schema: "Sales",
                table: "CustomerDebitMemos");

            migrationBuilder.DropColumn(
                name: "ReceivableAccountId",
                schema: "Sales",
                table: "CustomerDebitMemos");

            migrationBuilder.DropColumn(
                name: "DepositJournalEntryId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "DepositedDate",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "LegacyDocumentReferences",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "LegacyReceiptReference",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "MigrationBatchId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "OpeningReceivableAccountId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "OpeningSourceRow",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "ReceivedDate",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.AlterColumn<int>(
                name: "OriginalReceiptId",
                schema: "Finance",
                table: "ChequeRegister",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "ChequeRegister_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Finance")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.AddForeignKey(
                name: "FK_ChequeRegister_PaymentTransactions_OriginalReceiptId",
                schema: "Finance",
                table: "ChequeRegister",
                column: "OriginalReceiptId",
                principalSchema: "Finance",
                principalTable: "PaymentTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
