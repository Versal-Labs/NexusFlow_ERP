using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class products : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Accounts_CogsAccountId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Accounts_InventoryAccountId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Accounts_SalesAccountId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_CogsAccountId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_InventoryAccountId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SalesAccountId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CogsAccountId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "InventoryAccountId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SalesAccountId",
                schema: "Master",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                schema: "Master",
                table: "ProductVariants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "Master",
                table: "ProductVariants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumSellingPrice",
                schema: "Master",
                table: "ProductVariants",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MovingAverageCost",
                schema: "Master",
                table: "ProductVariants",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CogsAccountId",
                schema: "Master",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InventoryAccountId",
                schema: "Master",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentCategoryId",
                schema: "Master",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SalesAccountId",
                schema: "Master",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CogsAccountId",
                schema: "Master",
                table: "Categories",
                column: "CogsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_InventoryAccountId",
                schema: "Master",
                table: "Categories",
                column: "InventoryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId",
                schema: "Master",
                table: "Categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_SalesAccountId",
                schema: "Master",
                table: "Categories",
                column: "SalesAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Accounts_CogsAccountId",
                schema: "Master",
                table: "Categories",
                column: "CogsAccountId",
                principalSchema: "Finance",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Accounts_InventoryAccountId",
                schema: "Master",
                table: "Categories",
                column: "InventoryAccountId",
                principalSchema: "Finance",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Accounts_SalesAccountId",
                schema: "Master",
                table: "Categories",
                column: "SalesAccountId",
                principalSchema: "Finance",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                schema: "Master",
                table: "Categories",
                column: "ParentCategoryId",
                principalSchema: "Master",
                principalTable: "Categories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Accounts_CogsAccountId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Accounts_InventoryAccountId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Accounts_SalesAccountId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CogsAccountId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_InventoryAccountId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategoryId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_SalesAccountId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Barcode",
                schema: "Master",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "Master",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "MinimumSellingPrice",
                schema: "Master",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "MovingAverageCost",
                schema: "Master",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "CogsAccountId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "InventoryAccountId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ParentCategoryId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "SalesAccountId",
                schema: "Master",
                table: "Categories");

            migrationBuilder.AddColumn<int>(
                name: "CogsAccountId",
                schema: "Master",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InventoryAccountId",
                schema: "Master",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SalesAccountId",
                schema: "Master",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CogsAccountId",
                schema: "Master",
                table: "Products",
                column: "CogsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_InventoryAccountId",
                schema: "Master",
                table: "Products",
                column: "InventoryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SalesAccountId",
                schema: "Master",
                table: "Products",
                column: "SalesAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Accounts_CogsAccountId",
                schema: "Master",
                table: "Products",
                column: "CogsAccountId",
                principalSchema: "Finance",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Accounts_InventoryAccountId",
                schema: "Master",
                table: "Products",
                column: "InventoryAccountId",
                principalSchema: "Finance",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Accounts_SalesAccountId",
                schema: "Master",
                table: "Products",
                column: "SalesAccountId",
                principalSchema: "Finance",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
