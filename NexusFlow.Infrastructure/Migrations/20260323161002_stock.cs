using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class stock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "Inventory",
                table: "StockTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsExhausted",
                schema: "Inventory",
                table: "StockLayers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "Inventory",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "IsExhausted",
                schema: "Inventory",
                table: "StockLayers");
        }
    }
}
