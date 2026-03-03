using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsSubcontractor",
                schema: "Master",
                table: "Warehouses",
                newName: "IsActive");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "Master",
                table: "Warehouses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "LinkedSupplierId",
                schema: "Master",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerName",
                schema: "Master",
                table: "Warehouses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OverrideInventoryAccountId",
                schema: "Master",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "Master",
                table: "Warehouses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_LinkedSupplierId",
                schema: "Master",
                table: "Warehouses",
                column: "LinkedSupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Suppliers_LinkedSupplierId",
                schema: "Master",
                table: "Warehouses",
                column: "LinkedSupplierId",
                principalSchema: "Purchasing",
                principalTable: "Suppliers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Suppliers_LinkedSupplierId",
                schema: "Master",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_LinkedSupplierId",
                schema: "Master",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "Master",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "LinkedSupplierId",
                schema: "Master",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ManagerName",
                schema: "Master",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "OverrideInventoryAccountId",
                schema: "Master",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "Master",
                table: "Warehouses");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                schema: "Master",
                table: "Warehouses",
                newName: "IsSubcontractor");
        }
    }
}
