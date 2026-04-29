using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class salesrp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_SalesRepId",
                schema: "Sales",
                table: "SalesInvoices",
                column: "SalesRepId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Employees_SalesRepId",
                schema: "Sales",
                table: "SalesInvoices",
                column: "SalesRepId",
                principalSchema: "HR",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Employees_SalesRepId",
                schema: "Sales",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_SalesRepId",
                schema: "Sales",
                table: "SalesInvoices");
        }
    }
}
