using System.Text.RegularExpressions;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class InstallationPaths
    {
        public InstallationPaths()
        {
            var requestedId = Environment.GetEnvironmentVariable("NEXUSFLOW_INSTANCE_ID") ?? "default";
            InstanceId = Regex.Replace(requestedId, "[^A-Za-z0-9_.-]", "-");

            var rootOverride = Environment.GetEnvironmentVariable("NEXUSFLOW_INSTANCE_ROOT");
            RootPath = string.IsNullOrWhiteSpace(rootOverride)
                ? Path.Combine(DefaultBasePath(), "NexusFlow", "ERP", InstanceId)
                : Path.GetFullPath(rootOverride);

            StateFilePath = Path.Combine(RootPath, "installation-state.json");
            SecretFilePath = Path.Combine(RootPath, "secrets.dat");
            SecretKeyPath = Path.Combine(RootPath, "secret.key");
            BootstrapKeyPath = Path.Combine(RootPath, "bootstrap.key");
            DataProtectionKeysPath = Path.Combine(RootPath, "data-protection");
            LogsPath = Path.Combine(RootPath, "logs");
            StoragePath = Path.Combine(RootPath, "storage");
        }

        public string InstanceId { get; }
        public string RootPath { get; }
        public string StateFilePath { get; }
        public string SecretFilePath { get; }
        public string SecretKeyPath { get; }
        public string BootstrapKeyPath { get; }
        public string DataProtectionKeysPath { get; }
        public string LogsPath { get; }
        public string StoragePath { get; }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(DataProtectionKeysPath);
            Directory.CreateDirectory(LogsPath);
            Directory.CreateDirectory(StoragePath);
        }

        private static string DefaultBasePath()
        {
            if (OperatingSystem.IsWindows())
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            }

            return Environment.GetEnvironmentVariable("HOME")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                ?? Path.GetTempPath();
        }
    }
}
