using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierBillToAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocations_SalesInvoices_SalesInvoiceId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.AlterColumn<int>(
                name: "SalesInvoiceId",
                schema: "Finance",
                table: "PaymentAllocations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "SupplierBillId",
                schema: "Finance",
                table: "PaymentAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_SupplierBillId",
                schema: "Finance",
                table: "PaymentAllocations",
                column: "SupplierBillId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocations_SalesInvoices_SalesInvoiceId",
                schema: "Finance",
                table: "PaymentAllocations",
                column: "SalesInvoiceId",
                principalSchema: "Sales",
                principalTable: "SalesInvoices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocations_SupplierBills_SupplierBillId",
                schema: "Finance",
                table: "PaymentAllocations",
                column: "SupplierBillId",
                principalSchema: "Purchasing",
                principalTable: "SupplierBills",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocations_SalesInvoices_SalesInvoiceId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocations_SupplierBills_SupplierBillId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAllocations_SupplierBillId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.DropColumn(
                name: "SupplierBillId",
                schema: "Finance",
                table: "PaymentAllocations");

            migrationBuilder.AlterColumn<int>(
                name: "SalesInvoiceId",
                schema: "Finance",
                table: "PaymentAllocations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocations_SalesInvoices_SalesInvoiceId",
                schema: "Finance",
                table: "PaymentAllocations",
                column: "SalesInvoiceId",
                principalSchema: "Sales",
                principalTable: "SalesInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
