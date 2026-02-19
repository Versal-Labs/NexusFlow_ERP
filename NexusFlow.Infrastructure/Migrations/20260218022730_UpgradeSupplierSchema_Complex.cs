using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeSupplierSchema_Complex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.RenameColumn(
                name: "DefaultCreditPeriodDays",
                schema: "Purchasing",
                table: "Suppliers",
                newName: "SupplierGroupId");

            migrationBuilder.AlterColumn<string>(
                name: "TaxRegNo",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ContactPerson",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AccountsEmail",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankBranch",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankIBAN",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankSwiftCode",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessRegNo",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                schema: "Purchasing",
                table: "Suppliers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DefaultExpenseAccountId",
                schema: "Purchasing",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalNotes",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "Purchasing",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PaymentTermId",
                schema: "Purchasing",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RatingId",
                schema: "Purchasing",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TradeName",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Website",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_DefaultPayableAccountId",
                schema: "Purchasing",
                table: "Suppliers",
                column: "DefaultPayableAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Email",
                schema: "Purchasing",
                table: "Suppliers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_IsActive",
                schema: "Purchasing",
                table: "Suppliers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                schema: "Purchasing",
                table: "Suppliers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TaxRegNo",
                schema: "Purchasing",
                table: "Suppliers",
                column: "TaxRegNo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Accounts_DefaultPayableAccountId",
                schema: "Purchasing",
                table: "Suppliers",
                column: "DefaultPayableAccountId",
                principalSchema: "Finance",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Accounts_DefaultPayableAccountId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_DefaultPayableAccountId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Email",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_IsActive",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Name",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TaxRegNo",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AccountsEmail",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BankBranch",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BankIBAN",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BankName",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BankSwiftCode",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BusinessRegNo",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DefaultExpenseAccountId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "InternalNotes",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Mobile",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "RatingId",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TradeName",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Website",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                schema: "Purchasing",
                table: "Suppliers");

            migrationBuilder.RenameColumn(
                name: "SupplierGroupId",
                schema: "Purchasing",
                table: "Suppliers",
                newName: "DefaultCreditPeriodDays");

            migrationBuilder.AlterColumn<string>(
                name: "TaxRegNo",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "ContactPerson",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "Purchasing",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
