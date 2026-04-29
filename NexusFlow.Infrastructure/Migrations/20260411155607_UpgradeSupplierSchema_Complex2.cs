using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeSupplierSchema_Complex2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankBranch",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.RenameColumn(
                name: "State",
                schema: "Purchasing",
                table: "Suppliers",
                newName: "Province");

            migrationBuilder.RenameColumn(
                name: "BankName",
                schema: "Purchasing",
                table: "Suppliers",
                newName: "District");

            migrationBuilder.AddColumn<int>(
                name: "BankBranchId",
                schema: "Purchasing",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BankId",
                schema: "Purchasing",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BankBranchId",
                schema: "Purchasing",
                table: "Suppliers",
                column: "BankBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BankId",
                schema: "Purchasing",
                table: "Suppliers",
                column: "BankId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_BankBranches_BankBranchId",
                schema: "Purchasing",
                table: "Suppliers",
                column: "BankBranchId",
                principalSchema: "Finance",
                principalTable: "BankBranches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Banks_BankId",
                schema: "Purchasing",
                table: "Suppliers",
                column: "BankId",
                principalSchema: "Finance",
                principalTable: "Banks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_BankBranches_BankBranchId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Banks_BankId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BankBranchId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BankId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BankBranchId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BankId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.RenameColumn(
                name: "Province",
                schema: "Purchasing",
                table: "Suppliers",
                newName: "State");

            migrationBuilder.RenameColumn(
                name: "District",
                schema: "Purchasing",
                table: "Suppliers",
                newName: "BankName");

            migrationBuilder.AddColumn<string>(
                name: "BankBranch",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
