using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class InstallationConnectionStringProvider : IInstallationConnectionStringProvider
    {
        public const string DefaultConnectionSecret = "ConnectionStrings.DefaultConnection";
        public const string HangfireConnectionSecret = "Hangfire.ConnectionString";
        public const string JwtSecret = "JwtSettings.Secret";
        public const string SyncfusionLicenseSecret = "Syncfusion.LicenseKey";
        public const string AzureBlobStorageSecret = "ConnectionStrings.AzureBlobStorage";

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
