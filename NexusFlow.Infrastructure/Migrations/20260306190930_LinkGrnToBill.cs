using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkGrnToBill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBilled",
                schema: "Purchasing",
                table: "GRNs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SupplierBillId",
                schema: "Purchasing",
                table: "GRNs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GRNs_SupplierBillId",
                schema: "Purchasing",
                table: "GRNs",
                column: "SupplierBillId");

            migrationBuilder.AddForeignKey(
                name: "FK_GRNs_SupplierBills_SupplierBillId",
                schema: "Purchasing",
                table: "GRNs",
                column: "SupplierBillId",
                principalSchema: "Purchasing",
                principalTable: "SupplierBills",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GRNs_SupplierBills_SupplierBillId",
                schema: "Purchasing",
                table: "GRNs");

            migrationBuilder.DropIndex(
                name: "IX_GRNs_SupplierBillId",
                schema: "Purchasing",
                table: "GRNs");

            migrationBuilder.DropColumn(
                name: "IsBilled",
                schema: "Purchasing",
                table: "GRNs");

            migrationBuilder.DropColumn(
                name: "SupplierBillId",
                schema: "Purchasing",
                table: "GRNs");
        }
    }
}
