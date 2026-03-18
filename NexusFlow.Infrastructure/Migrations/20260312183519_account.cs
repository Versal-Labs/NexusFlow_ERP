using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class account : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "Finance",
                table: "Accounts",
                columns: new[] { "Id", "Balance", "Code", "CreatedAt", "CreatedBy", "IsTransactionAccount", "LastModifiedAt", "LastModifiedBy", "Name", "ParentAccountId", "Type" },
                values: new object[,]
                {
                    { 1, 0m, "1000", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Assets", null, 1 },
                    { 2, 0m, "2000", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Liabilities", null, 2 },
                    { 3, 0m, "3000", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Equity", null, 3 },
                    { 4, 0m, "4000", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Revenue", null, 4 },
                    { 5, 0m, "6000", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Operating Expenses", null, 5 },
                    { 500, 0m, "5000", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Cost of Goods Sold", null, 5 },
                    { 101, 0m, "1100", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Current Assets", 1, 1 },
                    { 201, 0m, "2100", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Current Liabilities", 2, 2 },
                    { 401, 0m, "4100", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Operating Revenue", 4, 4 },
                    { 601, 0m, "6200", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Administrative Expenses", 5, 5 },
                    { 602, 0m, "6300", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Sales & Marketing", 5, 5 },
                    { 3001, 0m, "3100", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Share Capital", 3, 3 },
                    { 3002, 0m, "3200", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Retained Earnings", 3, 3 },
                    { 5001, 0m, "5110", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Raw Material Consumption", 500, 5 },
                    { 5002, 0m, "5120", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Outsourced Job Work Costs", 500, 5 },
                    { 8, 0m, "1130", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Inventory Assets", 101, 1 },
                    { 9, 0m, "4110", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Sales Revenue - FG", 401, 4 },
                    { 10, 0m, "6210", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Rent Expense", 601, 5 },
                    { 11, 0m, "6220", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Electricity & Utilities", 601, 5 },
                    { 12, 0m, "6310", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Advertising & Marketing", 602, 5 },
                    { 102, 0m, "1110", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Cash & Cash Equivalents", 101, 1 },
                    { 202, 0m, "2120", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Tax Payable", 201, 2 },
                    { 1001, 0m, "1120", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Accounts Receivable (AR)", 101, 1 },
                    { 2001, 0m, "2110", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Accounts Payable (AP)", 201, 2 },
                    { 6, 0m, "1111", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Cash in Hand", 102, 1 },
                    { 7, 0m, "1112", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Bank - Sampath", 102, 1 },
                    { 13, 0m, "2121", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "VAT Payable", 202, 2 },
                    { 14, 0m, "2122", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "SSCL Payable", 202, 2 },
                    { 1002, 0m, "1131", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Raw Materials (RM) Inventory", 8, 1 },
                    { 1003, 0m, "1132", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Finished Goods (FG) Inventory", 8, 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 2001);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 3001);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 3002);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 5001);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 5002);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 202);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 401);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 500);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 601);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 602);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 201);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "Finance",
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
