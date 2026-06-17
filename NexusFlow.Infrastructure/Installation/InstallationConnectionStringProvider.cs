using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Installation;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class InstallationConnectionStringProvider : IInstallationConnectionStringProvider
    {
        public const string DefaultConnectionSecret = InstallationSecretKeys.DefaultConnection;
        public const string HangfireConnectionSecret = InstallationSecretKeys.HangfireConnection;
        public const string JwtSecret = InstallationSecretKeys.JwtSecret;
        public const string SyncfusionLicenseSecret = InstallationSecretKeys.SyncfusionLicense;
        public const string AzureBlobStorageSecret = InstallationSecretKeys.AzureBlobStorage;

        private readonly IInstallationSecretStore _secrets;

        public InstallationConnectionStringProvider(IInstallationSecretStore secrets)
        {
            _secrets = secrets;
        }

        public string? GetConnectionString() => _secrets.Get(DefaultConnectionSecret);

        public string GetRequiredConnectionString()
        {
            return GetConnectionString()
                ?? throw new InvalidOperationException("The NexusFlow database connection has not been configured.");
        }
    }
}
