using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class InstallationDatabaseProvisioner : IInstallationDatabaseProvisioner
    {
        private readonly IInstallationConnectionStringProvider _connectionStrings;

        public InstallationDatabaseProvisioner(IInstallationConnectionStringProvider connectionStrings)
        {
            _connectionStrings = connectionStrings;
        }

        public string BuildConnectionString(DatabaseConnectionRequest request)
        {
            if (request.UsePreconfiguredConnectionString ||
                !string.IsNullOrWhiteSpace(request.ConnectionString))
            {
                return request.ConnectionString
                    ?? _connectionStrings.GetRequiredConnectionString();
            }

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = request.Server.Trim(),
                InitialCatalog = request.Database.Trim(),
                IntegratedSecurity = request.UseWindowsAuthentication,
                Encrypt = true,
                TrustServerCertificate = request.TrustServerCertificate,
                ConnectTimeout = 15,
                MultipleActiveResultSets = true,
                ApplicationName = "NexusFlow ERP Installer"
            };

            if (!request.UseWindowsAuthentication)
            {
                builder.UserID = request.Username?.Trim();
                builder.Password = request.Password;
            }

            return builder.ConnectionString;
        }

        public async Task<DatabaseValidationResult> ValidateAsync(
            DatabaseConnectionRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var connectionString = BuildConnectionString(request);
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                await using var permissionCommand = connection.CreateCommand();
                permissionCommand.CommandText =
                    "SELECT HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE'), " +
                    "HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'ALTER')";

                await using var permissionReader = await permissionCommand.ExecuteReaderAsync(cancellationToken);
                await permissionReader.ReadAsync(cancellationToken);
                var canCreate = permissionReader.GetInt32(0) == 1;
                var canAlter = permissionReader.GetInt32(1) == 1;
                await permissionReader.CloseAsync();

                if (!canCreate || !canAlter)
                {
                    return new(false, DatabaseClassification.UnsupportedOrForeign,
                        "The database login requires CREATE TABLE and ALTER permissions during installation and upgrades.",
                        Array.Empty<string>());
                }

                var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using var tableCommand = connection.CreateCommand();
                tableCommand.CommandText =
                    "SELECT s.name + '.' + t.name FROM sys.tables t INNER JOIN sys.schemas s ON s.schema_id = t.schema_id";
                await using var tableReader = await tableCommand.ExecuteReaderAsync(cancellationToken);
                while (await tableReader.ReadAsync(cancellationToken))
                {
                    tableNames.Add(tableReader.GetString(0));
                }

                var classification = tableNames.Count == 0
                    ? DatabaseClassification.Empty
                    : tableNames.Contains("System.InstallationRecords")
                        ? DatabaseClassification.InstalledNexusFlow
                        : tableNames.Contains("Identity.Users") && tableNames.Contains("Finance.Accounts")
                            ? DatabaseClassification.CompatibleNexusFlow
                            : DatabaseClassification.UnsupportedOrForeign;

                var pending = classification == DatabaseClassification.UnsupportedOrForeign
                    ? Array.Empty<string>()
                    : await GetPendingMigrationsAsync(connectionString, cancellationToken);

                var succeeded = classification != DatabaseClassification.UnsupportedOrForeign;
                var message = succeeded
                    ? $"Database classified as {classification}."
                    : "The selected database contains an unrecognized schema. NexusFlow will not modify it.";

                return new(succeeded, classification, message, pending);
            }
            catch (Exception ex)
            {
                return new(false, DatabaseClassification.UnsupportedOrForeign,
                    $"Database connection failed: {ex.Message}", Array.Empty<string>());
            }
        }

        public async Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            await using var lockConnection = new SqlConnection(connectionString);
            await lockConnection.OpenAsync(cancellationToken);

            await using var acquireCommand = lockConnection.CreateCommand();
            acquireCommand.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = N'NexusFlow.InstallationOrUpgrade',
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Session',
                    @LockTimeout = 60000;
                SELECT @result;
                """;
            var lockResult = Convert.ToInt32(await acquireCommand.ExecuteScalarAsync(cancellationToken));
            if (lockResult < 0)
            {
                throw new InvalidOperationException(
                    "Another NexusFlow installation or upgrade is already running. Wait for it to finish and try again.");
            }

            try
            {
                await using var context = CreateContext(connectionString);
                await context.Database.MigrateAsync(cancellationToken);
            }
            finally
            {
                await using var releaseCommand = lockConnection.CreateCommand();
                releaseCommand.CommandText = """
                    EXEC sys.sp_releaseapplock
                        @Resource = N'NexusFlow.InstallationOrUpgrade',
                        @LockOwner = N'Session';
                    """;
                await releaseCommand.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }

        public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
        {
            return GetPendingMigrationsAsync(_connectionStrings.GetRequiredConnectionString(), cancellationToken);
        }

        private static async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
            string connectionString,
            CancellationToken cancellationToken)
        {
            await using var context = CreateContext(connectionString);
            return (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        }

        private static ErpDbContext CreateContext(string connectionString)
        {
            var options = new DbContextOptionsBuilder<ErpDbContext>()
                .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName))
                .Options;
            return new ErpDbContext(options, new SystemCurrentUserService());
        }
    }
}
