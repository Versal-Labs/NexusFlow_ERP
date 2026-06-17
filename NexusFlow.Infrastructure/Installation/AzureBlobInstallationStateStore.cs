using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class AzureBlobInstallationStateStore : IInstallationStateStore
    {
        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
        private readonly BlobContainerClient _containerClient;
        private readonly BlobClient _blobClient;
        private readonly string _instanceId;
        private readonly object _sync = new();

        public AzureBlobInstallationStateStore(
            string? connectionString,
            string containerName,
            string blobName,
            string instanceId)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Azure Blob Storage connection is required for the Azure installation state store.");
            }

            _containerClient = new BlobContainerClient(connectionString, containerName);
            _blobClient = _containerClient.GetBlobClient(blobName);
            _instanceId = instanceId;
        }

        public InstallationState Get()
        {
            lock (_sync)
            {
                try
                {
                    _containerClient.CreateIfNotExists();
                    if (!_blobClient.Exists())
                    {
                        return new InstallationState { InstanceId = _instanceId };
                    }

                    var content = _blobClient.DownloadContent().Value.Content.ToString();
                    return JsonSerializer.Deserialize<InstallationState>(content, JsonOptions)
                        ?? new InstallationState { InstanceId = _instanceId };
                }
                catch (RequestFailedException ex)
                {
                    return new InstallationState
                    {
                        InstanceId = _instanceId,
                        Mode = ApplicationMode.Faulted,
                        LastError = $"Azure installation state is unavailable: {ex.Message}"
                    };
                }
                catch
                {
                    return new InstallationState
                    {
                        InstanceId = _instanceId,
                        Mode = ApplicationMode.Faulted,
                        LastError = "Installation state is unreadable."
                    };
                }
            }
        }

        public async Task SaveAsync(InstallationState state, CancellationToken cancellationToken = default)
        {
            state.InstanceId = _instanceId;
            state.UpdatedAtUtc = DateTimeOffset.UtcNow;
            var json = JsonSerializer.Serialize(state, JsonOptions);

            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await _blobClient.UploadAsync(BinaryData.FromString(json), overwrite: true, cancellationToken);
        }

        public async Task EnsureInitializedAsync(string? setupKey = null, CancellationToken cancellationToken = default)
        {
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            if (await _blobClient.ExistsAsync(cancellationToken))
            {
                return;
            }

            setupKey ??= Environment.GetEnvironmentVariable("NEXUSFLOW_SETUP_KEY");

            var state = new InstallationState
            {
                InstanceId = _instanceId,
                Mode = ApplicationMode.Uninitialized
            };

            if (!string.IsNullOrWhiteSpace(setupKey))
            {
                var salt = RandomNumberGenerator.GetBytes(32);
                state.SetupKeySalt = Convert.ToBase64String(salt);
                state.SetupKeyHash = Convert.ToBase64String(HashSetupKey(setupKey, salt));
            }

            await SaveAsync(state, cancellationToken);
        }

        public bool VerifySetupKey(string setupKey)
        {
            var state = Get();
            if (state.SetupKeyConsumed ||
                string.IsNullOrWhiteSpace(state.SetupKeySalt) ||
                string.IsNullOrWhiteSpace(state.SetupKeyHash) ||
                string.IsNullOrWhiteSpace(setupKey))
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(state.SetupKeySalt);
                var expected = Convert.FromBase64String(state.SetupKeyHash);
                var actual = HashSetupKey(setupKey, salt);
                return CryptographicOperations.FixedTimeEquals(expected, actual);
            }
            catch
            {
                return false;
            }
        }

        public async Task ConsumeSetupKeyAsync(CancellationToken cancellationToken = default)
        {
            var state = Get();
            state.SetupKeyConsumed = true;
            state.SetupKeyHash = string.Empty;
            state.SetupKeySalt = string.Empty;
            await SaveAsync(state, cancellationToken);
        }

        private static byte[] HashSetupKey(string setupKey, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(setupKey),
                salt,
                210_000,
                HashAlgorithmName.SHA256,
                32);
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new CompatibleDateTimeOffsetConverter());
            return options;
        }

        private sealed class CompatibleDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
        {
            private static readonly Regex LegacyPowerShellDate =
                new(@"^/Date\((?<milliseconds>-?\d+)(?:[+-]\d+)?\)/$", RegexOptions.Compiled);

            public override DateTimeOffset Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                var value = reader.GetString();
                if (DateTimeOffset.TryParse(value, out var parsed))
                {
                    return parsed;
                }

                var match = LegacyPowerShellDate.Match(value ?? string.Empty);
                if (match.Success && long.TryParse(match.Groups["milliseconds"].Value, out var milliseconds))
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
                }

                throw new JsonException("Installation state contains an invalid timestamp.");
            }

            public override void Write(
                Utf8JsonWriter writer,
                DateTimeOffset value,
                JsonSerializerOptions options)
            {
                writer.WriteStringValue(value);
            }
        }
    }
}
