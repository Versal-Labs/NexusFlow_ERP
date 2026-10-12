using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NexusFlow.IntegrationTests;

public sealed class MigrationCentreUpgradeTests
{
    [CustomerMigrationSqlFact]
    public async Task IdempotentUpgrade_FromSupportedBaselines_PreservesTemporalTables()
    {
        foreach (var baseline in new[] { "20260622171142_FinancialControlsDocumentsAndProduction", "20261005014028_HardenCustomerMigrationControls" })
        {
            await using var database = new CustomerMigrationTestDatabase(); await using var db = database.Open();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(baseline);
            var script = migrator.GenerateScript(baseline, options: MigrationsSqlGenerationOptions.Idempotent);
            await using var connection = new SqlConnection(database.ConnectionString); await connection.OpenAsync();
            foreach (var pass in new[] { 1, 2 })
                foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    using var command = connection.CreateCommand(); command.CommandText = batch; command.CommandTimeout = 120; await command.ExecuteNonQueryAsync();
                }
            (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
            db.Database.HasPendingModelChanges().Should().BeFalse();
            using var check = connection.CreateCommand(); check.CommandText = "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE t.name IN ('MigrationProjects','MigrationUnits','SupplierOpeningItems','OpeningBankItems') AND t.temporal_type=2";
            Convert.ToInt32(await check.ExecuteScalarAsync()).Should().Be(4);
        }
    }
}
