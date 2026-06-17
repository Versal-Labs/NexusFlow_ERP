using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class EncryptedFileInstallationSecretStore : IInstallationSecretStore, IInstallationSecretStoreDiagnostics
    {
        private const string SecretKeyEnvironmentVariable = "NEXUSFLOW_SECRET_KEY";
        private const string SecretKeyEnvironmentVariableAlias = "NEXUSFLOW_SECRET_ENCRYPTION_KEY";
        private const int KeySizeBytes = 32;
        private const int NonceSizeBytes = 12;
        private const int TagSizeBytes = 16;
        private readonly InstallationPaths _paths;
        private readonly object _sync = new();

        public EncryptedFileInstallationSecretStore(InstallationPaths paths)
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

        public InstallationSecretDiagnostic Inspect(string key)
        {
            lock (_sync)
            {
                var values = ReadAll();
                if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    return new InstallationSecretDiagnostic
                    {
                        CanWrite = true,
                        Source = "Missing"
                    };
                }

                return new InstallationSecretDiagnostic
                {
                    HasValue = true,
                    HasStoredValue = true,
                    CanWrite = true,
                    Source = "Secret store",
                    Fingerprint = InstallationSecretFingerprint.Create(value)
                };
            }
        }

        private Dictionary<string, string> ReadAll()
        {
            if (!File.Exists(_paths.SecretFilePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var envelope = JsonSerializer.Deserialize<SecretEnvelope>(File.ReadAllText(_paths.SecretFilePath));
            if (envelope == null ||
                string.IsNullOrWhiteSpace(envelope.Nonce) ||
                string.IsNullOrWhiteSpace(envelope.CipherText) ||
                string.IsNullOrWhiteSpace(envelope.Tag))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var nonce = Convert.FromBase64String(envelope.Nonce);
            var cipherText = Convert.FromBase64String(envelope.CipherText);
            var tag = Convert.FromBase64String(envelope.Tag);
            var clearBytes = new byte[cipherText.Length];

            using var aes = new AesGcm(GetOrCreateKey(), TagSizeBytes);
            aes.Decrypt(nonce, cipherText, tag, clearBytes);

            return JsonSerializer.Deserialize<Dictionary<string, string>>(clearBytes)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private void WriteAll(Dictionary<string, string> values)
        {
            var clearBytes = JsonSerializer.SerializeToUtf8Bytes(values);
            var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var cipherText = new byte[clearBytes.Length];
            var tag = new byte[TagSizeBytes];

            using var aes = new AesGcm(GetOrCreateKey(), TagSizeBytes);
            aes.Encrypt(nonce, clearBytes, cipherText, tag);

            var envelope = new SecretEnvelope
            {
                Nonce = Convert.ToBase64String(nonce),
                CipherText = Convert.ToBase64String(cipherText),
                Tag = Convert.ToBase64String(tag)
            };
            var temporaryPath = _paths.SecretFilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(envelope));
            File.Move(temporaryPath, _paths.SecretFilePath, true);
        }

        private byte[] GetOrCreateKey()
        {
            var configuredKey = Environment.GetEnvironmentVariable(SecretKeyEnvironmentVariable)
                ?? Environment.GetEnvironmentVariable(SecretKeyEnvironmentVariableAlias);
            if (!string.IsNullOrWhiteSpace(configuredKey))
            {
                return DecodeKey(configuredKey);
            }

            if (File.Exists(_paths.SecretKeyPath))
            {
                return Convert.FromBase64String(File.ReadAllText(_paths.SecretKeyPath).Trim());
            }

            var key = RandomNumberGenerator.GetBytes(KeySizeBytes);
            File.WriteAllText(_paths.SecretKeyPath, Convert.ToBase64String(key));
            return key;
        }

        private static byte[] DecodeKey(string value)
        {
            try
            {
                var decoded = Convert.FromBase64String(value);
                if (decoded.Length == KeySizeBytes)
                {
                    return decoded;
                }
            }
            catch (FormatException)
            {
                // Treat non-base64 values as passphrases below.
            }

            return SHA256.HashData(Encoding.UTF8.GetBytes(value));
        }

        private sealed class SecretEnvelope
        {
            public string Nonce { get; init; } = string.Empty;
            public string CipherText { get; init; } = string.Empty;
            public string Tag { get; init; } = string.Empty;
        }
    }
}
