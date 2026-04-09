using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class coa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "Finance",
                table: "Accounts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemAccount",
                schema: "Finance",
                table: "Accounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresReconciliation",
                schema: "Finance",
                table: "Accounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 7,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 8,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 9,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 11,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 12,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 13,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 14,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 101,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 102,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 201,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 202,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 401,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 500,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 601,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 602,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 1001,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 1002,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 1003,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 2001,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 3001,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 3002,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 5001,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 5002,
                column: "IsActive",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "Finance",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "IsSystemAccount",
                schema: "Finance",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "RequiresReconciliation",
                schema: "Finance",
                table: "Accounts");
        }
    }
}
