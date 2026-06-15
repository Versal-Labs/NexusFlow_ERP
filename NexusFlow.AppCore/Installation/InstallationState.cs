namespace NexusFlow.AppCore.Installation
{
    public sealed class InstallationState
    {
        public string InstanceId { get; set; } = string.Empty;
        public ApplicationMode Mode { get; set; } = ApplicationMode.Uninitialized;
        public string ProductVersion { get; set; } = "1.0.0";
        public string TemplateVersion { get; set; } = string.Empty;
        public string SetupKeySalt { get; set; } = string.Empty;
        public string SetupKeyHash { get; set; } = string.Empty;
        public bool SetupKeyConsumed { get; set; }
        public string? LastError { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
