using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class DpapiInstallationSecretStore : IInstallationSecretStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NexusFlow.ERP.InstallationSecrets.v1");
        private readonly InstallationPaths _paths;
        private readonly object _sync = new();

        public DpapiInstallationSecretStore(InstallationPaths paths)
        {
            _paths = paths;
            _paths.EnsureDirectories();
        }

        public string? Get(string key)
        {
            lock (_sync)
            {
                var values = ReadAll();
                return values.TryGetValue(key, out var value) ? value : null;
            }
        }

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                var values = ReadAll();
                values[key] = value;
                WriteAll(values);
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                var values = ReadAll();
                if (values.Remove(key))
                {
                    WriteAll(values);
                }
            }

            return Task.CompletedTask;
        }

        private Dictionary<string, string> ReadAll()
        {
            if (!File.Exists(_paths.SecretFilePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var protectedBytes = File.ReadAllBytes(_paths.SecretFilePath);
            var clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(clearBytes)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private void WriteAll(Dictionary<string, string> values)
        {
            var clearBytes = JsonSerializer.SerializeToUtf8Bytes(values);
            var protectedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
            var temporaryPath = _paths.SecretFilePath + ".tmp";
            File.WriteAllBytes(temporaryPath, protectedBytes);
            File.Move(temporaryPath, _paths.SecretFilePath, true);
        }
    }
}
