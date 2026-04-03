using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class supplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_CustomerId",
                schema: "Finance",
                table: "PaymentTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_SupplierId",
                schema: "Finance",
                table: "PaymentTransactions",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_Customers_CustomerId",
                schema: "Finance",
                table: "PaymentTransactions",
                column: "CustomerId",
                principalSchema: "Sales",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_Suppliers_SupplierId",
                schema: "Finance",
                table: "PaymentTransactions",
                column: "SupplierId",
                principalSchema: "Purchasing",
                principalTable: "Suppliers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_Customers_CustomerId",
                schema: "Finance",
                table: "PaymentTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_Suppliers_SupplierId",
                schema: "Finance",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_CustomerId",
                schema: "Finance",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_SupplierId",
                schema: "Finance",
                table: "PaymentTransactions");
        }
    }
}
