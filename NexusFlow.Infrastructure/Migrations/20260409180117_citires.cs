using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class citires : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankBranch",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankSwiftCode",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "BankName",
                schema: "Sales",
                table: "Customers",
                newName: "Province");

            migrationBuilder.AddColumn<int>(
                name: "BankBranchId",
                schema: "Sales",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BankId",
                schema: "Sales",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Provinces",
                schema: "Master",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Districts",
                schema: "Master",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProvinceId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Districts_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalSchema: "Master",
                        principalTable: "Provinces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                schema: "Master",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DistrictId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cities_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalSchema: "Master",
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_BankBranchId",
                schema: "Sales",
                table: "Customers",
                column: "BankBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_BankId",
                schema: "Sales",
                table: "Customers",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_DistrictId",
                schema: "Master",
                table: "Cities",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Districts_ProvinceId",
                schema: "Master",
                table: "Districts",
                column: "ProvinceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_BankBranches_BankBranchId",
                schema: "Sales",
                table: "Customers",
                column: "BankBranchId",
                principalSchema: "Finance",
                principalTable: "BankBranches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Banks_BankId",
                schema: "Sales",
                table: "Customers",
                column: "BankId",
                principalSchema: "Finance",
                principalTable: "Banks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_BankBranches_BankBranchId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Banks_BankId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "Cities",
                schema: "Master");

            migrationBuilder.DropTable(
                name: "Districts",
                schema: "Master");

            migrationBuilder.DropTable(
                name: "Provinces",
                schema: "Master");

            migrationBuilder.DropIndex(
                name: "IX_Customers_BankBranchId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_BankId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankBranchId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankId",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "District",
                schema: "Sales",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "Province",
                schema: "Sales",
                table: "Customers",
                newName: "BankName");

            migrationBuilder.AddColumn<string>(
                name: "BankBranch",
                schema: "Sales",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
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
        }
    }
}
