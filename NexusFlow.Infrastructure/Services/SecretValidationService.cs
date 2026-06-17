using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Features.System.Secrets;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Infrastructure.Installation;

namespace NexusFlow.Infrastructure.Services
{
    public sealed class SecretValidationService : ISecretValidationService
    {
        private readonly InstallationRuntimeOptions _runtimeOptions;

        public SecretValidationService(InstallationRuntimeOptions runtimeOptions)
        {
            _runtimeOptions = runtimeOptions;
        }

        public async Task<SecretValidationResultDto> ValidateAsync(
            SecretDefinition definition,
            string value,
            CancellationToken cancellationToken = default)
        {
            return definition.Kind switch
            {
                SecretSettingKind.Database => await ValidateSqlConnectionAsync(value, cancellationToken),
                SecretSettingKind.Hangfire => string.IsNullOrWhiteSpace(value)
                    ? Ok("Hangfire will inherit the primary database connection after restart.")
                    : await ValidateSqlConnectionAsync(value, cancellationToken),
                SecretSettingKind.AzureBlobStorage => await ValidateAzureBlobAsync(value, cancellationToken),
                SecretSettingKind.SyncfusionLicense => ValidateSyncfusionLicense(value),
                SecretSettingKind.JwtSecret => ValidateJwtSecret(value),
                _ => Fail("Unknown secret type.")
            };
        }

        private static async Task<SecretValidationResultDto> ValidateSqlConnectionAsync(
            string connectionString,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Fail("Database connection string is required.");
            }

            try
            {
                await using var connection = new SqlConnection(connectionString.Trim());
                await connection.OpenAsync(cancellationToken);

                var warnings = new List<string>();
                if (!await LooksLikeNexusFlowDatabaseAsync(connection, cancellationToken))
                {
                    warnings.Add("The connection is reachable, but NexusFlow identity/system tables were not detected. This may be a new database or the wrong database.");
                }

                var pendingMigrations = await GetPendingMigrationsAsync(connectionString.Trim(), cancellationToken);
                if (pendingMigrations.Count > 0)
                {
                    warnings.Add($"{pendingMigrations.Count} EF Core migration(s) are pending on this database.");
                }

                return Ok("SQL connection test succeeded.", warnings);
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException or ArgumentException)
            {
                return Fail($"SQL connection test failed: {ex.Message}");
            }
        }

        private static async Task<bool> LooksLikeNexusFlowDatabaseAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT COUNT_BIG(1)
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE (s.name = 'Identity' AND t.name = 'Users')
                   OR (s.name = 'System' AND t.name = 'AuditLogs')
                """;

            await using var command = new SqlCommand(sql, connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result) >= 1;
        }

        private static async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
            string connectionString,
            CancellationToken cancellationToken)
        {
            try
            {
                var options = new DbContextOptionsBuilder<ErpDbContext>()
                    .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName))
                    .Options;

                await using var context = new ErpDbContext(options, new SystemCurrentUserService());
                return (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private async Task<SecretValidationResultDto> ValidateAzureBlobAsync(
            string connectionString,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Fail("Azure Blob Storage connection string is required.");
            }

            if (string.IsNullOrWhiteSpace(_runtimeOptions.AzureBlobStorageContainer))
            {
                return Fail("Azure Blob Storage container name could not be resolved for this instance.");
            }

            try
            {
                var serviceClient = new BlobServiceClient(connectionString.Trim());
                var container = serviceClient.GetBlobContainerClient(_runtimeOptions.AzureBlobStorageContainer);
                await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

                var probeName = $"installation/probes/secret-vault-{Guid.NewGuid():N}.txt";
                var blob = container.GetBlobClient(probeName);
                await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("NexusFlow Azure Blob probe"));
                await blob.UploadAsync(stream, overwrite: true, cancellationToken);
                await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);

                return Ok($"Azure Blob Storage test succeeded for container '{_runtimeOptions.AzureBlobStorageContainer}'.");
            }
            catch (Exception ex) when (ex is RequestFailedException or FormatException or ArgumentException or InvalidOperationException)
            {
                return Fail($"Azure Blob Storage test failed: {ex.Message}");
            }
        }

        private static SecretValidationResultDto ValidateSyncfusionLicense(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Fail("Syncfusion license key is required.");
            }

            if (value.Length > 20000)
            {
                return Fail("Syncfusion license key is too long.");
            }

            return Ok(
                "Syncfusion license key format was accepted. Full license validation happens when Syncfusion components initialize after restart.",
                ["Restart and render a PDF/Word document to confirm the license is accepted by Syncfusion."]);
        }

        private static SecretValidationResultDto ValidateJwtSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Fail("JWT secret is required.");
            }

            var trimmed = value.Trim();
            try
            {
                var decoded = Convert.FromBase64String(trimmed);
                if (decoded.Length >= 32)
                {
                    return Ok("JWT secret has sufficient entropy.");
                }
            }
            catch (FormatException)
            {
                // Fall back to raw byte length below.
            }

            return Encoding.UTF8.GetByteCount(trimmed) >= 32
                ? Ok("JWT secret length is acceptable.", ["Generated base64 secrets are recommended for production."])
                : Fail("JWT secret must be at least 32 bytes. Use the rotate action to generate a secure 64-byte secret.");
        }

        private static SecretValidationResultDto Ok(string message, IEnumerable<string>? warnings = null)
        {
            return new SecretValidationResultDto
            {
                IsValid = true,
                Message = message,
                Warnings = warnings?.ToList() ?? []
            };
        }

        private static SecretValidationResultDto Fail(string message)
        {
            return new SecretValidationResultDto
            {
                IsValid = false,
                Message = message
            };
        }
    }
}
