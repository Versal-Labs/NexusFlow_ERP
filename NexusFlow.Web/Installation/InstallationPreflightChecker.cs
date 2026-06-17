using System.Security.Cryptography;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Infrastructure.Installation;

namespace NexusFlow.Web.Installation
{
    public interface IInstallationPreflightChecker
    {
        IReadOnlyList<ReadinessCheck> Check(HttpRequest request);
    }

    public sealed class InstallationPreflightChecker : IInstallationPreflightChecker
    {
        private readonly InstallationPaths _paths;
        private readonly InstallationRuntimeOptions _options;
        private readonly IInstallationConnectionStringProvider _connectionStrings;

        public InstallationPreflightChecker(
            InstallationPaths paths,
            InstallationRuntimeOptions options,
            IInstallationConnectionStringProvider connectionStrings)
        {
            _paths = paths;
            _options = options;
            _connectionStrings = connectionStrings;
        }

        public IReadOnlyList<ReadinessCheck> Check(HttpRequest request)
        {
            var checks = new List<ReadinessCheck>
            {
                new("preflight.https", "Installer is accessed over HTTPS", request.IsHttps,
                    request.IsHttps ? null : "Configure HTTPS for the selected hosting profile."),
                new("preflight.profile", $"Deployment profile is {_options.Profile}", true),
                CheckStateStore(),
                CheckSecretStore(),
                CheckDataProtectionStore(),
                CheckStorageMode()
            };

            if (_options.Profile == DeploymentProfile.AzureAppService)
            {
                var databaseConnectionConfigured = !string.IsNullOrWhiteSpace(_connectionStrings.GetConnectionString());
                checks.Add(new(
                    "preflight.database-secret",
                    "Azure App Service database connection string is preconfigured",
                    databaseConnectionConfigured,
                    databaseConnectionConfigured ? null : "Set ConnectionStrings__DefaultConnection, SQLCONNSTR_DefaultConnection, or a Key Vault-referenced App Service connection string."));
            }

            if (UsesLocalFileSystem())
            {
                checks.Add(CheckWritableDirectory(_paths.RootPath));
                checks.Add(CheckDiskSpace(_paths.RootPath));
            }

            if (_options.SecretStoreMode == InstallationSecretStoreMode.Dpapi)
            {
                checks.Add(CheckWindows());
                checks.Add(CheckDpapi());
            }

            return checks;
        }

        private ReadinessCheck CheckStateStore()
        {
            if (_options.StateStoreMode == InstallationStateStoreMode.AzureBlob)
            {
                return CheckAzureConnection("preflight.state-store", "Azure Blob installation state store is configured");
            }

            return new("preflight.state-store", "File installation state store is configured", true);
        }

        private ReadinessCheck CheckSecretStore()
        {
            return _options.SecretStoreMode switch
            {
                InstallationSecretStoreMode.Dpapi => new("preflight.secret-store", "DPAPI secret store is selected", OperatingSystem.IsWindows(),
                    OperatingSystem.IsWindows() ? null : "DPAPI can only be used on Windows. Select EncryptedFile or Environment for portable deployments."),
                InstallationSecretStoreMode.Environment => new("preflight.secret-store", "Environment/Key Vault read-through secret store is selected", true),
                _ => new("preflight.secret-store", "Cross-platform encrypted file secret store is selected", true)
            };
        }

        private ReadinessCheck CheckDataProtectionStore()
        {
            if (_options.DataProtectionStoreMode == DataProtectionStoreMode.AzureBlob)
            {
                return CheckAzureConnection("preflight.data-protection", "Azure Blob Data Protection key ring is configured");
            }

            return new("preflight.data-protection", "File Data Protection key ring is configured", true);
        }

        private ReadinessCheck CheckStorageMode()
        {
            if (_options.StorageMode == StorageMode.Local)
            {
                return new("preflight.storage-mode", "Local storage mode is configured", true);
            }

            var mode = _options.StorageMode == StorageMode.AzureBlob ? "Azure Blob" : "Hybrid";
            return CheckAzureConnection("preflight.storage-mode", $"{mode} storage mode is configured");
        }

        private ReadinessCheck CheckAzureConnection(string key, string description)
        {
            var configured = !string.IsNullOrWhiteSpace(_options.AzureBlobStorageConnectionString);
            return new(key, description, configured,
                configured ? null : "Configure ConnectionStrings__AzureBlobStorage or a Key Vault-referenced App Service setting.");
        }

        private bool UsesLocalFileSystem()
        {
            return _options.StateStoreMode == InstallationStateStoreMode.File ||
                   _options.SecretStoreMode is InstallationSecretStoreMode.Dpapi or InstallationSecretStoreMode.EncryptedFile ||
                   _options.DataProtectionStoreMode == DataProtectionStoreMode.File ||
                   _options.StorageMode is StorageMode.Local or StorageMode.Hybrid;
        }

        private static ReadinessCheck CheckWindows()
        {
            return new("preflight.windows", "Windows hosting environment is available", OperatingSystem.IsWindows(),
                OperatingSystem.IsWindows() ? null : "DPAPI secret protection requires Windows.");
        }

        private static ReadinessCheck CheckWritableDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                var probe = Path.Combine(path, $".preflight-{Guid.NewGuid():N}");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return new("preflight.directories", "Instance directories are writable", true);
            }
            catch (Exception ex)
            {
                return new("preflight.directories", "Instance directories are writable", false, ex.Message);
            }
        }

        private static ReadinessCheck CheckDiskSpace(string path)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(path))!;
                var freeBytes = new DriveInfo(root).AvailableFreeSpace;
                var passed = freeBytes >= 1024L * 1024 * 1024;
                return new("preflight.disk", "At least 1 GB free disk space is available", passed,
                    passed ? null : $"{freeBytes / 1024 / 1024:N0} MB is available.");
            }
            catch (Exception ex)
            {
                return new("preflight.disk", "At least 1 GB free disk space is available", false, ex.Message);
            }
        }

        private static ReadinessCheck CheckDpapi()
        {
            if (!OperatingSystem.IsWindows())
                return new("preflight.dpapi", "DPAPI encryption works for the app-pool identity", false, "Windows is required.");

            try
            {
                var clear = RandomNumberGenerator.GetBytes(32);
                var encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return new("preflight.dpapi", "DPAPI encryption works for the app-pool identity",
                    CryptographicOperations.FixedTimeEquals(clear, decrypted));
            }
            catch (Exception ex)
            {
                return new("preflight.dpapi", "DPAPI encryption works for the app-pool identity", false, ex.Message);
            }
        }
    }
}
