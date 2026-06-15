using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class InstallationOrchestrator : IInstallationOrchestrator
    {
        private static readonly SemaphoreSlim InstallationLock = new(1, 1);
        private readonly IInstallationStateStore _stateStore;
        private readonly IInstallationSecretStore _secretStore;
        private readonly IInstallationDatabaseProvisioner _databaseProvisioner;
        private readonly InstallationPaths _paths;
        private readonly IServiceScopeFactory _scopeFactory;

        public InstallationOrchestrator(
            IInstallationStateStore stateStore,
            IInstallationSecretStore secretStore,
            IInstallationDatabaseProvisioner databaseProvisioner,
            InstallationPaths paths,
            IServiceScopeFactory scopeFactory)
        {
            _stateStore = stateStore;
            _secretStore = secretStore;
            _databaseProvisioner = databaseProvisioner;
            _paths = paths;
            _scopeFactory = scopeFactory;
        }

        public async Task<InstallationResult> InstallAsync(
            InstallationRequest request,
            CancellationToken cancellationToken = default)
        {
            await InstallationLock.WaitAsync(cancellationToken);
            try
            {
                var current = _stateStore.Get();
                if (current.Mode == ApplicationMode.Installed && current.SetupKeyConsumed)
                {
                    return new(false, "This NexusFlow instance is already installed.");
                }

                var validationError = ValidateRequest(request);
                if (validationError != null)
                {
                    return new(false, validationError);
                }

                request.LocalStoragePath = string.IsNullOrWhiteSpace(request.LocalStoragePath)
                    ? _paths.StoragePath
                    : Path.GetFullPath(request.LocalStoragePath);

                var databaseValidation = await _databaseProvisioner.ValidateAsync(request.Database, cancellationToken);
                if (!databaseValidation.Succeeded)
                {
                    return new(false, databaseValidation.Message);
                }

                var connectionString = _databaseProvisioner.BuildConnectionString(request.Database);
                await _secretStore.SetAsync(InstallationConnectionStringProvider.DefaultConnectionSecret, connectionString, cancellationToken);
                await _secretStore.SetAsync(InstallationConnectionStringProvider.HangfireConnectionSecret, connectionString, cancellationToken);
                if (string.IsNullOrWhiteSpace(_secretStore.Get(InstallationConnectionStringProvider.JwtSecret)))
                {
                    await _secretStore.SetAsync(
                        InstallationConnectionStringProvider.JwtSecret,
                        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                        cancellationToken);
                }

                current.Mode = ApplicationMode.Installing;
                current.LastError = null;
                await _stateStore.SaveAsync(current, cancellationToken);

                await _databaseProvisioner.ApplyMigrationsAsync(connectionString, cancellationToken);

                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var template = scope.ServiceProvider.GetRequiredService<IInstallationTemplateProvider>();
                    await template.ApplyAsync(request, cancellationToken);
                }

                ReadinessReport readiness;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    readiness = await scope.ServiceProvider
                        .GetRequiredService<IInstallationReadinessChecker>()
                        .CheckAsync(cancellationToken);
                }

                if (!readiness.IsReady)
                {
                    current = _stateStore.Get();
                    current.Mode = ApplicationMode.Installing;
                    current.LastError = "Readiness checks failed.";
                    await _stateStore.SaveAsync(current, cancellationToken);
                    return new(false, "Installation completed its provisioning steps, but readiness checks failed.", readiness);
                }

                await MarkDatabaseInstallationCompleteAsync(cancellationToken);
                await _stateStore.ConsumeSetupKeyAsync(cancellationToken);
                current = _stateStore.Get();
                current.Mode = ApplicationMode.Installed;
                current.TemplateVersion = "lk-light-manufacturing-1.0";
                current.LastError = null;
                await _stateStore.SaveAsync(current, cancellationToken);

                return new(true, "NexusFlow installation completed successfully.", readiness);
            }
            catch (Exception ex)
            {
                var state = _stateStore.Get();
                state.Mode = ApplicationMode.Installing;
                state.LastError = SanitizeError(ex.Message);
                await _stateStore.SaveAsync(state, CancellationToken.None);
                return new(false, $"Installation failed and can be resumed: {SanitizeError(ex.Message)}");
            }
            finally
            {
                InstallationLock.Release();
            }
        }

        public async Task<InstallationResult> UpgradeAsync(CancellationToken cancellationToken = default)
        {
            await InstallationLock.WaitAsync(cancellationToken);
            try
            {
                var state = _stateStore.Get();
                state.Mode = ApplicationMode.UpgradeRequired;
                state.LastError = null;
                await _stateStore.SaveAsync(state, cancellationToken);

                var connectionString = _secretStore.Get(InstallationConnectionStringProvider.DefaultConnectionSecret)
                    ?? throw new InvalidOperationException("Database connection is not configured.");
                await _databaseProvisioner.ApplyMigrationsAsync(connectionString, cancellationToken);

                ReadinessReport readiness;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    readiness = await scope.ServiceProvider
                        .GetRequiredService<IInstallationReadinessChecker>()
                        .CheckAsync(cancellationToken);
                }

                if (!readiness.IsReady)
                {
                    state.LastError = "Upgrade migrations applied, but readiness checks failed.";
                    await _stateStore.SaveAsync(state, cancellationToken);
                    return new(false, state.LastError, readiness);
                }

                state.Mode = ApplicationMode.Installed;
                state.LastError = null;
                await _stateStore.SaveAsync(state, cancellationToken);
                return new(true, "Upgrade completed successfully.", readiness);
            }
            catch (Exception ex)
            {
                var state = _stateStore.Get();
                state.Mode = ApplicationMode.UpgradeRequired;
                state.LastError = SanitizeError(ex.Message);
                await _stateStore.SaveAsync(state, CancellationToken.None);
                return new(false, $"Upgrade failed: {SanitizeError(ex.Message)}");
            }
            finally
            {
                InstallationLock.Release();
            }
        }

        private async Task MarkDatabaseInstallationCompleteAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var record = await context.InstallationRecords.FirstAsync(cancellationToken);
            record.Status = "Installed";
            record.CompletedAtUtc = DateTimeOffset.UtcNow;
            record.SchemaVersion = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault() ?? "unknown";
            await context.SaveChangesAsync(cancellationToken);
        }

        private static string? ValidateRequest(InstallationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Database.Server) || string.IsNullOrWhiteSpace(request.Database.Database))
                return "SQL Server and database name are required.";
            if (!request.Database.UseWindowsAuthentication &&
                (string.IsNullOrWhiteSpace(request.Database.Username) || string.IsNullOrWhiteSpace(request.Database.Password)))
                return "SQL username and password are required when Windows Authentication is not selected.";
            if (string.IsNullOrWhiteSpace(request.CompanyName) || string.IsNullOrWhiteSpace(request.CanonicalUrl))
                return "Company name and canonical URL are required.";
            if (!Uri.TryCreate(request.CanonicalUrl, UriKind.Absolute, out var canonicalUrl) || canonicalUrl.Scheme != Uri.UriSchemeHttps)
                return "Canonical URL must be an absolute HTTPS URL.";
            if (request.FiscalYearEnd <= request.FiscalYearStart)
                return "Fiscal year end must be after fiscal year start.";
            if (string.IsNullOrWhiteSpace(request.AdminFullName) || string.IsNullOrWhiteSpace(request.AdminEmail))
                return "Initial SuperAdmin name and email are required.";
            if (request.AdminPassword.Length < 12)
                return "Initial SuperAdmin password must contain at least 12 characters.";
            return null;
        }

        private static string SanitizeError(string message)
        {
            if (message.Contains("Password=", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("User ID=", StringComparison.OrdinalIgnoreCase))
            {
                return "A database operation failed. Review the server-side logs.";
            }

            return message;
        }
    }
}
