namespace NexusFlow.AppCore.Installation
{
    public sealed record DatabaseConnectionRequest(
        string Server,
        string Database,
        bool UseWindowsAuthentication,
        string? Username,
        string? Password,
        bool TrustServerCertificate);

    public sealed record DatabaseValidationResult(
        bool Succeeded,
        DatabaseClassification Classification,
        string Message,
        IReadOnlyList<string> PendingMigrations);

    public sealed class InstallationRequest
    {
        public DatabaseConnectionRequest Database { get; set; } =
            new(string.Empty, string.Empty, true, null, null, false);

        public string CompanyName { get; set; } = string.Empty;
        public string TaxRegistrationNumber { get; set; } = string.Empty;
        public string CanonicalUrl { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = "Sri Lanka Standard Time";
        public DateTime FiscalYearStart { get; set; } = new(DateTime.UtcNow.Year, 1, 1);
        public DateTime FiscalYearEnd { get; set; } = new(DateTime.UtcNow.Year, 12, 31);
        public decimal VatRate { get; set; } = 18m;
        public decimal SsclRate { get; set; } = 2.5m;
        public string WarehouseCode { get; set; } = "WH-MAIN";
        public string WarehouseName { get; set; } = "Main Warehouse";
        public string WarehouseLocation { get; set; } = string.Empty;
        public string LocalStoragePath { get; set; } = string.Empty;
        public string AdminFullName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }

    public sealed record ReadinessCheck(string Code, string Description, bool Passed, string? Detail = null);

    public sealed class ReadinessReport
    {
        public IReadOnlyList<ReadinessCheck> Checks { get; init; } = Array.Empty<ReadinessCheck>();
        public bool IsReady => Checks.Count > 0 && Checks.All(x => x.Passed);
    }

    public sealed record InstallationResult(bool Succeeded, string Message, ReadinessReport? Readiness = null);
}
