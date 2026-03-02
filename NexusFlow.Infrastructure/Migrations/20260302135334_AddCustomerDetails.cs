using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.AlterColumn<string>(
                name: "TaxRegNo",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankBranch",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankSwiftCode",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPerson",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CreditPeriodDays",
                schema: "Sales",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CustomerGroupId",
                schema: "Sales",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultReceivableAccountId",
                schema: "Sales",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultRevenueAccountId",
                schema: "Sales",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InternalNotes",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "Sales",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PaymentTermId",
                schema: "Sales",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PriceLevelId",
                schema: "Sales",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SalesRepId",
                schema: "Sales",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeName",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLine1",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankBranch",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankName",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankSwiftCode",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ContactPerson",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreditPeriodDays",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerGroupId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DefaultReceivableAccountId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DefaultRevenueAccountId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "InternalNotes",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Mobile",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PriceLevelId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SalesRepId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TradeName",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.AlterColumn<string>(
                name: "TaxRegNo",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
