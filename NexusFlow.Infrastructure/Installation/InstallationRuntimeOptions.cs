using Microsoft.Extensions.Configuration;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class InstallationRuntimeOptions
    {
        public DeploymentProfile Profile { get; init; }
        public InstallationStateStoreMode StateStoreMode { get; init; }
        public InstallationSecretStoreMode SecretStoreMode { get; init; }
        public DataProtectionStoreMode DataProtectionStoreMode { get; init; }
        public StorageMode StorageMode { get; init; }
        public string? AzureBlobStorageConnectionString { get; init; }
        public string? AzureBlobStateContainer { get; init; }
        public string? AzureBlobDataProtectionContainer { get; init; }
        public string? AzureBlobStorageContainer { get; init; }
        public string? StateBlobName { get; init; }
        public string? DataProtectionBlobName { get; init; }
        public bool UsesCloudState => StateStoreMode == InstallationStateStoreMode.AzureBlob;
        public bool UsesCloudDataProtection => DataProtectionStoreMode == DataProtectionStoreMode.AzureBlob;
        public bool UsesAzureStorage => StorageMode is StorageMode.AzureBlob or StorageMode.Hybrid;
    }

    public sealed class InstallationRuntime
    {
        private InstallationRuntime(
            InstallationPaths paths,
            InstallationRuntimeOptions options,
            IInstallationStateStore stateStore,
            IInstallationSecretStore secretStore)
        {
            Paths = paths;
            Options = options;
            StateStore = stateStore;
            SecretStore = secretStore;
        }

        public InstallationPaths Paths { get; }
        public InstallationRuntimeOptions Options { get; }
        public IInstallationStateStore StateStore { get; }
        public IInstallationSecretStore SecretStore { get; }

        public static InstallationRuntime Create(IConfiguration configuration)
        {
            var paths = new InstallationPaths();
            paths.EnsureDirectories();

            var options = InstallationRuntimeOptionsFactory.Create(configuration, paths);
            var fileState = new JsonInstallationStateStore(paths);
            IInstallationStateStore stateStore = options.StateStoreMode == InstallationStateStoreMode.AzureBlob
                ? new AzureBlobInstallationStateStore(
                    options.AzureBlobStorageConnectionString,
                    options.AzureBlobStateContainer!,
                    options.StateBlobName!,
                    paths.InstanceId)
                : fileState;

            IInstallationSecretStore secretStore = options.SecretStoreMode switch
            {
                InstallationSecretStoreMode.Dpapi => new DpapiInstallationSecretStore(paths),
                InstallationSecretStoreMode.Environment => new CompositeInstallationSecretStore(
                    new EnvironmentInstallationSecretStore(configuration),
                    new EncryptedFileInstallationSecretStore(paths)),
                _ => new EncryptedFileInstallationSecretStore(paths)
            };

            return new InstallationRuntime(paths, options, stateStore, secretStore);
        }
    }

    public static class InstallationRuntimeOptionsFactory
    {
        public static InstallationRuntimeOptions Create(IConfiguration configuration, InstallationPaths paths)
        {
            var profile = ReadEnum(
                FirstConfigured(Environment.GetEnvironmentVariable("NEXUSFLOW_DEPLOYMENT_PROFILE"), configuration["Deployment:Profile"]),
                OperatingSystem.IsWindows() ? DeploymentProfile.WindowsIis : DeploymentProfile.PortableVm);

            var azureConnection = ReadConnectionString(configuration, InstallationConnectionStringProvider.AzureBlobStorageSecret);
            var storageModeDefault = profile == DeploymentProfile.AzureAppService
                ? StorageMode.AzureBlob
                : string.IsNullOrWhiteSpace(azureConnection) ? StorageMode.Local : StorageMode.Hybrid;

            var stateModeDefault = profile == DeploymentProfile.AzureAppService && !string.IsNullOrWhiteSpace(azureConnection)
                ? InstallationStateStoreMode.AzureBlob
                : InstallationStateStoreMode.File;

            var dataProtectionDefault = profile == DeploymentProfile.AzureAppService && !string.IsNullOrWhiteSpace(azureConnection)
                ? DataProtectionStoreMode.AzureBlob
                : DataProtectionStoreMode.File;

            var secretStoreDefault = profile switch
            {
                DeploymentProfile.WindowsIis when OperatingSystem.IsWindows() => InstallationSecretStoreMode.Dpapi,
                DeploymentProfile.AzureAppService => InstallationSecretStoreMode.Environment,
                _ => InstallationSecretStoreMode.EncryptedFile
            };

            var storageMode = ReadEnum(
                FirstConfigured(Environment.GetEnvironmentVariable("NEXUSFLOW_STORAGE_MODE"), configuration["Storage:Mode"]),
                storageModeDefault);
            var stateMode = ReadEnum(
                FirstConfigured(Environment.GetEnvironmentVariable("NEXUSFLOW_STATE_STORE"), configuration["Deployment:StateStore"]),
                stateModeDefault);
            var secretStoreMode = ReadEnum(
                FirstConfigured(Environment.GetEnvironmentVariable("NEXUSFLOW_SECRET_STORE"), configuration["Deployment:SecretStore"]),
                secretStoreDefault);
            var dataProtectionMode = ReadEnum(
                FirstConfigured(Environment.GetEnvironmentVariable("NEXUSFLOW_DATA_PROTECTION_STORE"), configuration["Deployment:DataProtectionStore"]),
                dataProtectionDefault);

            var tenantContainer = NormalizeContainerName(
                FirstConfigured(
                    Environment.GetEnvironmentVariable("NEXUSFLOW_STORAGE_CONTAINER"),
                    configuration["Storage:AzureContainer"],
                    $"tenant-{paths.InstanceId}")!);

            return new InstallationRuntimeOptions
            {
                Profile = profile,
                StateStoreMode = stateMode,
                SecretStoreMode = secretStoreMode,
                DataProtectionStoreMode = dataProtectionMode,
                StorageMode = storageMode,
                AzureBlobStorageConnectionString = azureConnection,
                AzureBlobStateContainer = NormalizeContainerName(
                    FirstConfigured(
                        Environment.GetEnvironmentVariable("NEXUSFLOW_STATE_CONTAINER"),
                        configuration["Deployment:AzureStateContainer"],
                        tenantContainer)!),
                AzureBlobDataProtectionContainer = NormalizeContainerName(
                    FirstConfigured(
                        Environment.GetEnvironmentVariable("NEXUSFLOW_DATA_PROTECTION_CONTAINER"),
                        configuration["Deployment:AzureDataProtectionContainer"],
                        tenantContainer)!),
                AzureBlobStorageContainer = tenantContainer,
                StateBlobName = FirstConfigured(
                    Environment.GetEnvironmentVariable("NEXUSFLOW_STATE_BLOB"),
                    configuration["Deployment:StateBlobName"],
                    "installation/installation-state.json"),
                DataProtectionBlobName = FirstConfigured(
                    Environment.GetEnvironmentVariable("NEXUSFLOW_DATA_PROTECTION_BLOB"),
                    configuration["Deployment:DataProtectionBlobName"],
                    "installation/data-protection-keys.xml")
            };
        }

        public static string? ReadConnectionString(IConfiguration configuration, string key)
        {
            var configValue = configuration[key] ?? configuration.GetConnectionString(ConnectionStringName(key));
            if (!string.IsNullOrWhiteSpace(configValue))
            {
                return configValue;
            }

            var simpleName = ConnectionStringName(key);
            return Environment.GetEnvironmentVariable(key.Replace(':', '_'))
                ?? Environment.GetEnvironmentVariable($"ConnectionStrings__{simpleName}")
                ?? Environment.GetEnvironmentVariable($"SQLCONNSTR_{simpleName}")
                ?? Environment.GetEnvironmentVariable($"CUSTOMCONNSTR_{simpleName}")
                ?? Environment.GetEnvironmentVariable(simpleName);
        }

        public static string NormalizeContainerName(string value)
        {
            var normalized = new string(value.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' ? ch : '-')
                .ToArray());
            normalized = string.Join("-", normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));

            if (normalized.Length < 3)
            {
                normalized = string.IsNullOrWhiteSpace(normalized)
                    ? "tenant-default"
                    : $"tenant-{normalized}";
            }

            normalized = normalized.Trim('-');
            if (normalized.Length < 3)
            {
                normalized = "tenant-default";
            }

            return normalized.Length <= 63 ? normalized : normalized[..63].TrimEnd('-');
        }

        private static string ConnectionStringName(string key)
        {
            var index = key.LastIndexOf('.');
            return index >= 0 ? key[(index + 1)..] : key;
        }

        private static string? FirstConfigured(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static TEnum ReadEnum<TEnum>(string? value, TEnum fallback)
            where TEnum : struct, Enum
        {
            return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
                ? parsed
                : fallback;
        }
    }
}
