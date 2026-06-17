using NexusFlow.AppCore.Installation;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IInstallationStateStore
    {
        InstallationState Get();
        Task SaveAsync(InstallationState state, CancellationToken cancellationToken = default);
        Task EnsureInitializedAsync(string? setupKey = null, CancellationToken cancellationToken = default);
        bool VerifySetupKey(string setupKey);
        Task ConsumeSetupKeyAsync(CancellationToken cancellationToken = default);
    }

    public interface IInstallationSecretStore
    {
        string? Get(string key);
        Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    }

    public sealed class InstallationSecretDiagnostic
    {
        public bool HasValue { get; init; }
        public bool HasStoredValue { get; init; }
        public bool HasPlatformValue { get; init; }
        public bool CanWrite { get; init; }
        public string Source { get; init; } = "Missing";
        public string? Fingerprint { get; init; }
    }

    public interface IInstallationSecretStoreDiagnostics
    {
        InstallationSecretDiagnostic Inspect(string key);
    }

    public interface IInstallationRuntimeContext
    {
        string InstanceId { get; }
        string DeploymentProfile { get; }
        string StorageMode { get; }
        string? AzureBlobStorageContainer { get; }
    }

    public interface ICurrentUserPasswordValidator
    {
        Task<bool> ValidateCurrentPasswordAsync(string password, CancellationToken cancellationToken = default);
    }

    public interface IApplicationRestartService
    {
        Task RequestRestartAsync(CancellationToken cancellationToken = default);
    }

    public interface IInstallationConnectionStringProvider
    {
        string? GetConnectionString();
        string GetRequiredConnectionString();
    }

    public interface IInstallationDatabaseProvisioner
    {
        string BuildConnectionString(DatabaseConnectionRequest request);
        Task<DatabaseValidationResult> ValidateAsync(DatabaseConnectionRequest request, CancellationToken cancellationToken = default);
        Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default);
    }

    public interface IInstallationTemplateProvider
    {
        string TemplateVersion { get; }
        Task ApplyAsync(InstallationRequest request, CancellationToken cancellationToken = default);
    }

    public interface IInstallationReadinessChecker
    {
        Task<ReadinessReport> CheckAsync(CancellationToken cancellationToken = default);
    }

    public interface IInstallationOrchestrator
    {
        Task<InstallationResult> InstallAsync(InstallationRequest request, CancellationToken cancellationToken = default);
        Task<InstallationResult> UpgradeAsync(CancellationToken cancellationToken = default);
    }
}
