using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class JsonInstallationStateStore : IInstallationStateStore
    {
        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
        private readonly InstallationPaths _paths;
        private readonly object _sync = new();

        public JsonInstallationStateStore(InstallationPaths paths)
        {
            _paths = paths;
            _paths.EnsureDirectories();
        }

        public InstallationState Get()
        {
            lock (_sync)
            {
                if (!File.Exists(_paths.StateFilePath))
                {
                    return new InstallationState { InstanceId = _paths.InstanceId };
                }

                try
                {
                    return JsonSerializer.Deserialize<InstallationState>(
                        File.ReadAllText(_paths.StateFilePath), JsonOptions)
                        ?? new InstallationState { InstanceId = _paths.InstanceId };
                }
                catch
                {
                    return new InstallationState
                    {
                        InstanceId = _paths.InstanceId,
                        Mode = ApplicationMode.Faulted,
                        LastError = "Installation state is unreadable."
                    };
                }
            }
        }

        public async Task SaveAsync(InstallationState state, CancellationToken cancellationToken = default)
        {
            state.InstanceId = _paths.InstanceId;
            state.UpdatedAtUtc = DateTimeOffset.UtcNow;
            var json = JsonSerializer.Serialize(state, JsonOptions);
            var temporaryPath = _paths.StateFilePath + ".tmp";

            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, _paths.StateFilePath, true);
        }

        public async Task EnsureInitializedAsync(string? setupKey = null, CancellationToken cancellationToken = default)
        {
            if (File.Exists(_paths.StateFilePath))
            {
                return;
            }

            setupKey ??= ReadBootstrapKey();
            setupKey ??= Environment.GetEnvironmentVariable("NEXUSFLOW_SETUP_KEY");

            var state = new InstallationState
            {
                InstanceId = _paths.InstanceId,
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

        private string? ReadBootstrapKey()
        {
            if (!File.Exists(_paths.BootstrapKeyPath))
            {
                return null;
            }

            var value = File.ReadAllText(_paths.BootstrapKeyPath).Trim();
            File.Delete(_paths.BootstrapKeyPath);
            return value;
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
