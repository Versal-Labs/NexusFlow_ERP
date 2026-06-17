using NexusFlow.AppCore.Installation;

namespace NexusFlow.AppCore.Features.System.Secrets
{
    public enum SecretSettingKind
    {
        Database = 0,
        Hangfire = 1,
        AzureBlobStorage = 2,
        SyncfusionLicense = 3,
        JwtSecret = 4
    }

    public sealed record SecretDefinition(
        string Key,
        string DisplayName,
        string Category,
        string Description,
        SecretSettingKind Kind,
        bool CanRemove,
        bool CanRotate,
        bool RequiresRestart,
        string InputMode);

    public static class SecretRegistry
    {
        public static IReadOnlyList<SecretDefinition> All { get; } =
        [
            new(
                InstallationSecretKeys.DefaultConnection,
                "Database Connection",
                "Database & Hangfire",
                "Primary SQL Server or Azure SQL connection string used by the ERP.",
                SecretSettingKind.Database,
                CanRemove: false,
                CanRotate: false,
                RequiresRestart: true,
                InputMode: "connectionString"),
            new(
                InstallationSecretKeys.HangfireConnection,
                "Hangfire Connection",
                "Database & Hangfire",
                "Optional Hangfire SQL connection string. Leave empty to inherit the primary database connection.",
                SecretSettingKind.Hangfire,
                CanRemove: true,
                CanRotate: false,
                RequiresRestart: true,
                InputMode: "connectionString"),
            new(
                InstallationSecretKeys.AzureBlobStorage,
                "Azure Blob Storage",
                "Azure Blob Storage",
                "Azure Storage connection string used for tenant files, templates, logos, state, and optional data protection.",
                SecretSettingKind.AzureBlobStorage,
                CanRemove: true,
                CanRotate: false,
                RequiresRestart: true,
                InputMode: "connectionString"),
            new(
                InstallationSecretKeys.SyncfusionLicense,
                "Syncfusion License",
                "Syncfusion License",
                "Syncfusion license key used by PDF, barcode, Excel, and Word rendering components.",
                SecretSettingKind.SyncfusionLicense,
                CanRemove: true,
                CanRotate: false,
                RequiresRestart: true,
                InputMode: "password"),
            new(
                InstallationSecretKeys.JwtSecret,
                "JWT Security Secret",
                "JWT Security Secret",
                "Signing secret used for API and realtime JWT token validation.",
                SecretSettingKind.JwtSecret,
                CanRemove: false,
                CanRotate: true,
                RequiresRestart: true,
                InputMode: "password")
        ];

        public static SecretDefinition? Find(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return All.FirstOrDefault(x => x.Key.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class SecretSettingsStatusDto
    {
        public string InstanceId { get; set; } = string.Empty;
        public string DeploymentProfile { get; set; } = string.Empty;
        public string StorageMode { get; set; } = string.Empty;
        public string? AzureBlobContainer { get; set; }
        public bool RestartRequired { get; set; }
        public string? RestartRequiredAtUtc { get; set; }
        public string? RestartRequiredReason { get; set; }
        public List<SecretSettingStatusItemDto> Items { get; set; } = [];
    }

    public sealed class SecretSettingStatusItemDto
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string InputMode { get; set; } = string.Empty;
        public bool Configured { get; set; }
        public string Source { get; set; } = "Missing";
        public string? Fingerprint { get; set; }
        public bool HasStoredValue { get; set; }
        public bool HasPlatformValue { get; set; }
        public bool PlatformOverrideActive { get; set; }
        public bool CanRemove { get; set; }
        public bool CanRotate { get; set; }
        public bool RequiresRestart { get; set; }
        public string? LastAuditAtUtc { get; set; }
        public string? Warning { get; set; }
    }

    public sealed class SecretValidationResultDto
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = [];
    }

    public sealed class SecretMutationResultDto
    {
        public bool RestartRequired { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class TestSecretSettingRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public sealed class SaveSecretSettingRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public sealed class RemoveSecretSettingRequest
    {
        public string Key { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public sealed class SecretPasswordConfirmationRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
    }
}
