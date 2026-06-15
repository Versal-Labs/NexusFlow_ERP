using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBarcodePrintingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BarcodeTemplates",
                schema: "Master",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PageWidthMM = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    PageHeightMM = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    StickerWidthMM = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    StickerHeightMM = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    StickersPerRow = table.Column<int>(type: "int", nullable: false),
                    RowsPerPage = table.Column<int>(type: "int", nullable: false),
                    MarginTopMM = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    MarginLeftMM = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    HorizontalGapMM = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    VerticalGapMM = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    Symbology = table.Column<int>(type: "int", nullable: false),
                    PrintCompanyName = table.Column<bool>(type: "bit", nullable: false),
                    PrintProductName = table.Column<bool>(type: "bit", nullable: false),
                    PrintPrice = table.Column<bool>(type: "bit", nullable: false),
                    PrintSizeColor = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BarcodeTemplates", x => x.Id);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "BarcodeTemplates_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Master")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "IX_BarcodeTemplates_IsDefault",
                schema: "Master",
                table: "BarcodeTemplates",
                column: "IsDefault",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BarcodeTemplates_Name",
                schema: "Master",
                table: "BarcodeTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue])
                SELECT r.[Id], 'Permission', permissions.[ClaimValue]
                FROM [Identity].[Roles] r
                CROSS APPLY (VALUES
                    ('Permissions.Inventory.PrintBarcodes'),
                    ('Permissions.Inventory.ManageBarcodeTemplates')
                ) permissions([ClaimValue])
                WHERE UPPER(r.[Name]) = 'ADMIN'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [Identity].[RoleClaims] existing
                      WHERE existing.[RoleId] = r.[Id]
                        AND existing.[ClaimType] = 'Permission'
                        AND existing.[ClaimValue] = permissions.[ClaimValue]
                  );

                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue])
                SELECT r.[Id], 'Permission', 'Permissions.Inventory.PrintBarcodes'
                FROM [Identity].[Roles] r
                WHERE UPPER(r.[Name]) = 'STOREKEEPER'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [Identity].[RoleClaims] existing
                      WHERE existing.[RoleId] = r.[Id]
                        AND existing.[ClaimType] = 'Permission'
                        AND existing.[ClaimValue] = 'Permissions.Inventory.PrintBarcodes'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE claims
                FROM [Identity].[RoleClaims] claims
                INNER JOIN [Identity].[Roles] roles ON roles.[Id] = claims.[RoleId]
                WHERE claims.[ClaimType] = 'Permission'
                  AND (
                    (UPPER(roles.[Name]) = 'ADMIN' AND claims.[ClaimValue] IN (
                        'Permissions.Inventory.PrintBarcodes',
                        'Permissions.Inventory.ManageBarcodeTemplates'
                    ))
                    OR
                    (UPPER(roles.[Name]) = 'STOREKEEPER' AND claims.[ClaimValue] = 'Permissions.Inventory.PrintBarcodes')
                  );
                """);

            migrationBuilder.DropTable(
                name: "BarcodeTemplates",
                schema: "Master")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "BarcodeTemplates_History")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Master")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");
        }
    }
}
