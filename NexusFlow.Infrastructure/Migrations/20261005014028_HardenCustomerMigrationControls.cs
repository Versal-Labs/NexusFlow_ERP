using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenCustomerMigrationControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Cheque_Origin",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.AddColumn<int>(
                name: "OpeningUndepositedAccountId",
                schema: "Finance",
                table: "ChequeRegister",
                type: "int",
                nullable: true);

            // Bind the new columns after ALTER TABLE has run in the script batch.
            migrationBuilder.Sql("EXEC(N'ALTER TABLE [Finance].[PaymentAllocations] ADD CONSTRAINT [CK_PaymentAllocation_Target] CHECK ((CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierBillId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END) = 1 AND AmountAllocated > 0);');");

            // Bind the new columns after ALTER TABLE has run in the script batch.
            migrationBuilder.Sql("EXEC(N'ALTER TABLE [Finance].[ChequeRegister] ADD CONSTRAINT [CK_Cheque_Origin] CHECK (([MigrationBatchId] IS NULL AND [OriginalReceiptId] IS NOT NULL) OR ([MigrationBatchId] IS NOT NULL AND [OriginalReceiptId] IS NULL AND [OpeningReceivableAccountId] IS NOT NULL AND [OpeningUndepositedAccountId] IS NOT NULL));');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocation_Target",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Cheque_Origin",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "OpeningUndepositedAccountId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Cheque_Origin",
                schema: "Finance",
                table: "ChequeRegister",
                sql: "([MigrationBatchId] IS NULL AND [OriginalReceiptId] IS NOT NULL) OR ([MigrationBatchId] IS NOT NULL AND [OriginalReceiptId] IS NULL AND [OpeningReceivableAccountId] IS NOT NULL)");
        }
    }
}
