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
    public async Task OperationUpgrade_PreservesFinalizedData_AndActualLegacyRunTemporalSetting()
    {
        foreach (var temporalOn in new[] { true, false })
        {
            await using var database = new CustomerMigrationTestDatabase(); await using var db = database.Open();
            var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261008142956_latest");
            // Raw baseline inserts allow this test to run before the additive tables exist.
            await db.Database.ExecuteSqlRawAsync("""
                INSERT Migration.MigrationProjects (Name,SourceSystem,OpeningDate,ClosingDate,Currency,Status,SettingsJson,Version,CreatedAt,FinalizedAt,FinalEvidence)
                VALUES ('Retained finalized fixture','UPGRADE','2026-10-01','2026-09-30','LKR','Finalized','{{}}',17,SYSUTCDATETIME(),SYSUTCDATETIME(),'Signed financial evidence');
                DECLARE @p int=SCOPE_IDENTITY();
                INSERT Migration.MigrationScopes (ProjectId,Kind,Code,Status,RequiredModulesJson,Evidence,ReleasedAt,CreatedAt)
                VALUES (@p,'Customer','RETAIN','Released','[]','Original release',SYSUTCDATETIME(),SYSUTCDATETIME());
                INSERT Migration.MigrationDatasets (ProjectId,Module,Revision,IsCurrent,FileName,FileHash,StorageReference,ClosingDate,RowsJson,ErrorsJson,CreatedAt)
                VALUES (@p,'ControlTotals',1,1,'retained.csv','HASH','fixture','2026-09-30','[]','[]',SYSUTCDATETIME());
                INSERT Migration.MigrationUnits (ProjectId,Module,ScopeKey,Hash,Status,RowsJson,CreatedAt)
                VALUES (@p,'ControlTotals','ALL','HASH','Posted','[]',SYSUTCDATETIME());
                INSERT Migration.MigrationRuns (ProjectId,ApprovedHash,Status,SelectionJson,CommittedUnits,CreatedAt)
                VALUES (@p,'HASH','Completed','{{"Selection":{{"Modules":["ControlTotals"]}},"Preview":{{"Units":[]}}}}',1,SYSUTCDATETIME());
                UPDATE Migration.MigrationRuns SET Error='Retained history fixture';
                """);
            if (!temporalOn) await db.Database.ExecuteSqlRawAsync("ALTER TABLE Migration.MigrationRuns SET (SYSTEM_VERSIONING=OFF)");
            var script = migrator.GenerateScript("20261008142956_latest", options: MigrationsSqlGenerationOptions.Idempotent);
            script.Should().NotContain("SYSTEM_VERSIONING"); script.Should().NotContain("DELETE FROM");
            await using var connection = new SqlConnection(database.ConnectionString); await connection.OpenAsync();
            for (var pass = 0; pass < 2; pass++)
                foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x)))
                { using var command = connection.CreateCommand(); command.CommandText = batch; command.CommandTimeout = 120; await command.ExecuteNonQueryAsync(); }
            using var setting = connection.CreateCommand(); setting.CommandText = "SELECT temporal_type FROM sys.tables WHERE object_id=OBJECT_ID('Migration.MigrationRuns')";
            Convert.ToInt32(await setting.ExecuteScalarAsync()).Should().Be(temporalOn ? 2 : 0);
            using var history = connection.CreateCommand(); history.CommandText = "SELECT COUNT(*) FROM Migration.MigrationRuns_History"; Convert.ToInt32(await history.ExecuteScalarAsync()).Should().Be(1);
            using var progress = connection.CreateCommand(); progress.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('Migration') AND name IN ('MigrationOperations','MigrationOperationItems','MigrationOperationPayloads','MigrationOperationItemPayloads','MigrationOperationIssues','MigrationRunPayloads') AND temporal_type=0";
            Convert.ToInt32(await progress.ExecuteScalarAsync()).Should().Be(6);
            var project = await db.MigrationProjects.SingleAsync(); project.Status.Should().Be("Finalized"); project.Version.Should().Be(17); project.FinalEvidence.Should().Be("Signed financial evidence");
            (await db.MigrationScopes.SingleAsync()).Status.Should().Be("Released"); (await db.MigrationDatasets.CountAsync()).Should().Be(1); (await db.MigrationUnits.CountAsync()).Should().Be(1); (await db.MigrationRuns.SingleAsync()).SelectionJson.Should().Contain("ControlTotals");
            db.Database.HasPendingModelChanges().Should().BeFalse();
        }
    }
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
