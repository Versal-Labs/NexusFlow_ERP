using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningStockNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Config].[NumberSequences]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [Config].[NumberSequences] WHERE [Module] = N'OpeningStock')
                BEGIN
                    INSERT INTO [Config].[NumberSequences]
                        ([Module], [Prefix], [NextNumber], [Delimiter], [LastUsed], [Suffix], [CreatedAt], [CreatedBy], [LastModifiedAt], [LastModifiedBy])
                    VALUES
                        (N'OpeningStock', N'OBSTK', 1, N'-', SYSUTCDATETIME(), N'', SYSUTCDATETIME(), N'System', NULL, NULL);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Config].[NumberSequences]', N'U') IS NOT NULL
                BEGIN
                    DELETE FROM [Config].[NumberSequences]
                    WHERE [Module] = N'OpeningStock'
                      AND [Prefix] = N'OBSTK';
                END
                """);
        }
    }
}
