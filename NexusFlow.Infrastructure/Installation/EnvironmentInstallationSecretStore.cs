using Microsoft.Extensions.Configuration;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class EnvironmentInstallationSecretStore : IInstallationSecretStore, IInstallationSecretStoreDiagnostics
    {
        private readonly IConfiguration _configuration;

        public EnvironmentInstallationSecretStore(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string? Get(string key)
        {
            return InstallationRuntimeOptionsFactory.ReadConnectionString(_configuration, key)
                ?? _configuration[key]
                ?? Environment.GetEnvironmentVariable(key.Replace(':', '_'))
                ?? Environment.GetEnvironmentVariable(key.Replace('.', '_'))
                ?? Environment.GetEnvironmentVariable(key);
        }

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public InstallationSecretDiagnostic Inspect(string key)
        {
            var value = Get(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                return new InstallationSecretDiagnostic
                {
                    Source = "Missing"
                };
            }

            return new InstallationSecretDiagnostic
            {
                HasValue = true,
                HasPlatformValue = true,
                Source = "Environment/platform",
                Fingerprint = InstallationSecretFingerprint.Create(value)
            };
        }
    }

    public sealed class CompositeInstallationSecretStore : IInstallationSecretStore, IInstallationSecretStoreDiagnostics
    {
        private readonly IInstallationSecretStore _primary;
        private readonly IInstallationSecretStore _fallback;

        public CompositeInstallationSecretStore(IInstallationSecretStore primary, IInstallationSecretStore fallback)
        {
            _primary = primary;
            _fallback = fallback;
        }

        public string? Get(string key) => _primary.Get(key) ?? _fallback.Get(key);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            return _fallback.SetAsync(key, value, cancellationToken);
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return _fallback.RemoveAsync(key, cancellationToken);
        }

        public InstallationSecretDiagnostic Inspect(string key)
        {
            var primary = Inspect(_primary, key);
            var fallback = Inspect(_fallback, key);
            var effective = primary.HasValue ? primary : fallback;

            return new InstallationSecretDiagnostic
            {
                HasValue = effective.HasValue,
                HasStoredValue = fallback.HasStoredValue || (fallback.HasValue && fallback.CanWrite),
                HasPlatformValue = primary.HasPlatformValue || (primary.HasValue && !primary.CanWrite),
                CanWrite = fallback.CanWrite,
                Source = effective.HasValue ? effective.Source : "Missing",
                Fingerprint = effective.Fingerprint
            };
        }

        private static InstallationSecretDiagnostic Inspect(IInstallationSecretStore store, string key)
        {
            if (store is IInstallationSecretStoreDiagnostics diagnostics)
            {
                return diagnostics.Inspect(key);
            }

            var value = store.Get(key);
            return string.IsNullOrWhiteSpace(value)
                ? new InstallationSecretDiagnostic { Source = "Missing" }
                : new InstallationSecretDiagnostic
                {
                    HasValue = true,
                    Source = "Secret store",
                    Fingerprint = InstallationSecretFingerprint.Create(value)
                };
        }
    }
}
