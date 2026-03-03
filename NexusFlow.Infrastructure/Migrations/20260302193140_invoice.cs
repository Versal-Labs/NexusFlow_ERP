using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class invoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApplyVat",
                schema: "Sales",
                table: "SalesInvoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "Sales",
                table: "SalesInvoices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SalesRepId",
                schema: "Sales",
                table: "SalesInvoices",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyVat",
                schema: "Sales",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "Sales",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SalesRepId",
                schema: "Sales",
                table: "SalesInvoices");
        }
    }
}
