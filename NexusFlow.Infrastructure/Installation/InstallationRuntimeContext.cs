using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class InstallationRuntimeContext : IInstallationRuntimeContext
    {
        private readonly InstallationPaths _paths;
        private readonly InstallationRuntimeOptions _options;

        public InstallationRuntimeContext(InstallationPaths paths, InstallationRuntimeOptions options)
        {
            _paths = paths;
            _options = options;
        }

        public string InstanceId => _paths.InstanceId;
        public string DeploymentProfile => _options.Profile.ToString();
        public string StorageMode => _options.StorageMode.ToString();
        public string? AzureBlobStorageContainer => _options.AzureBlobStorageContainer;
    }
}
