using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Brands_BrandId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_UnitOfMeasures_UnitOfMeasureId",
                schema: "Master",
                table: "Products");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Brands_BrandId",
                schema: "Master",
                table: "Products",
                column: "BrandId",
                principalSchema: "Master",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                schema: "Master",
                table: "Products",
                column: "CategoryId",
                principalSchema: "Master",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_UnitOfMeasures_UnitOfMeasureId",
                schema: "Master",
                table: "Products",
                column: "UnitOfMeasureId",
                principalSchema: "Master",
                principalTable: "UnitOfMeasures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Brands_BrandId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                schema: "Master",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_UnitOfMeasures_UnitOfMeasureId",
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

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Brands_BrandId",
                schema: "Master",
                table: "Products",
                column: "BrandId",
                principalSchema: "Master",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                schema: "Master",
                table: "Products",
                column: "CategoryId",
                principalSchema: "Master",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_UnitOfMeasures_UnitOfMeasureId",
                schema: "Master",
                table: "Products",
                column: "UnitOfMeasureId",
                principalSchema: "Master",
                principalTable: "UnitOfMeasures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
