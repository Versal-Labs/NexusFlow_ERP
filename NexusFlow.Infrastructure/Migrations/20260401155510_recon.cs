using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class recon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BankReconciliationId",
                schema: "Finance",
                table: "JournalLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCleared",
                schema: "Finance",
                table: "JournalLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BankReconciliations",
                schema: "Finance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    StatementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatementEndingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsFinalized = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankReconciliations_Accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "Finance",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_BankReconciliationId",
                schema: "Finance",
                table: "JournalLines",
                column: "BankReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_BankAccountId",
                schema: "Finance",
                table: "BankReconciliations",
                column: "BankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalLines_BankReconciliations_BankReconciliationId",
                schema: "Finance",
                table: "JournalLines",
                column: "BankReconciliationId",
                principalSchema: "Finance",
                principalTable: "BankReconciliations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalLines_BankReconciliations_BankReconciliationId",
                schema: "Finance",
                table: "JournalLines");

            migrationBuilder.DropTable(
                name: "BankReconciliations",
                schema: "Finance");

            migrationBuilder.DropIndex(
                name: "IX_JournalLines_BankReconciliationId",
                schema: "Finance",
                table: "JournalLines");

            migrationBuilder.DropColumn(
                name: "BankReconciliationId",
                schema: "Finance",
                table: "JournalLines");

            migrationBuilder.DropColumn(
                name: "IsCleared",
                schema: "Finance",
                table: "JournalLines");
        }
    }
}
