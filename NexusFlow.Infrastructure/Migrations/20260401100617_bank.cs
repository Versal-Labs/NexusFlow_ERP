using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class bank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankName",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.AddColumn<int>(
                name: "BankBranchId",
                schema: "Finance",
                table: "ChequeRegister",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Banks",
                schema: "Finance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SwiftCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankBranches",
                schema: "Finance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankId = table.Column<int>(type: "int", nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankBranches_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "Finance",
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChequeRegister_BankBranchId",
                schema: "Finance",
                table: "ChequeRegister",
                column: "BankBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BankBranches_BankId",
                schema: "Finance",
                table: "BankBranches",
                column: "BankId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChequeRegister_BankBranches_BankBranchId",
                schema: "Finance",
                table: "ChequeRegister",
                column: "BankBranchId",
                principalSchema: "Finance",
                principalTable: "BankBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChequeRegister_BankBranches_BankBranchId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropTable(
                name: "BankBranches",
                schema: "Finance");

            migrationBuilder.DropTable(
                name: "Banks",
                schema: "Finance");

            migrationBuilder.DropIndex(
                name: "IX_ChequeRegister_BankBranchId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.DropColumn(
                name: "BankBranchId",
                schema: "Finance",
                table: "ChequeRegister");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                schema: "Finance",
                table: "ChequeRegister",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
