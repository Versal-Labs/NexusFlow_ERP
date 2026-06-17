namespace NexusFlow.AppCore.Installation
{
    public static class InstallationSecretKeys
    {
        public const string DefaultConnection = "ConnectionStrings.DefaultConnection";
        public const string HangfireConnection = "Hangfire.ConnectionString";
        public const string AzureBlobStorage = "ConnectionStrings.AzureBlobStorage";
        public const string SyncfusionLicense = "Syncfusion.LicenseKey";
        public const string JwtSecret = "JwtSettings.Secret";

        public const string RestartRequiredAtUtc = "NexusFlow.Runtime.RestartRequiredAtUtc";
        public const string RestartRequiredReason = "NexusFlow.Runtime.RestartRequiredReason";
    }
}
