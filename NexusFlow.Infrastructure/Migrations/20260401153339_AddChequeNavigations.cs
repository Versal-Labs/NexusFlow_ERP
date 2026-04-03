using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChequeNavigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EndorsedPaymentId",
                schema: "Finance",
                table: "ChequeRegister",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChequeRegister_EndorsedPaymentId",
                schema: "Finance",
                table: "ChequeRegister",
                column: "EndorsedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChequeRegister_OriginalReceiptId",
                schema: "Finance",
                table: "ChequeRegister",
                column: "OriginalReceiptId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChequeRegister_PaymentTransactions_EndorsedPaymentId",
                schema: "Finance",
                table: "ChequeRegister",
                column: "EndorsedPaymentId",
                principalSchema: "Finance",
                principalTable: "PaymentTransactions",
                principalColumn: "Id");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChequeRegister_PaymentTransactions_EndorsedPaymentId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropForeignKey(
                name: "FK_ChequeRegister_PaymentTransactions_OriginalReceiptId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropIndex(
                name: "IX_ChequeRegister_EndorsedPaymentId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropIndex(
                name: "IX_ChequeRegister_OriginalReceiptId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "EndorsedPaymentId",
                schema: "Finance",
                table: "ChequeRegister");
        }
    }
}
