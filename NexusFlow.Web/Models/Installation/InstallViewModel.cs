using System.ComponentModel.DataAnnotations;
using NexusFlow.AppCore.Installation;

namespace NexusFlow.Web.Models.Installation
{
    public sealed class InstallViewModel
    {
        public ApplicationMode Mode { get; set; }
        public bool IsUnlocked { get; set; }
        public string? Error { get; set; }
        public string? SetupKey { get; set; }
        public IReadOnlyList<ReadinessCheck> ReadinessChecks { get; set; } = Array.Empty<ReadinessCheck>();
        public IReadOnlyList<ReadinessCheck> PreflightChecks { get; set; } = Array.Empty<ReadinessCheck>();

        public string Server { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public bool UseWindowsAuthentication { get; set; } = true;
        public bool UsePreconfiguredConnectionString { get; set; }
        public bool PreconfiguredConnectionStringAvailable { get; set; }
        public string DeploymentProfile { get; set; } = string.Empty;
        public string StorageMode { get; set; } = string.Empty;
        public string StateStoreMode { get; set; } = string.Empty;
        public string SecretStoreMode { get; set; } = string.Empty;
        public string? SqlUsername { get; set; }
        public string? SqlPassword { get; set; }
        public bool TrustServerCertificate { get; set; }

        [Required] public string CompanyName { get; set; } = string.Empty;
        [Required] public string TaxRegistrationNumber { get; set; } = string.Empty;
        [Required, Url] public string CanonicalUrl { get; set; } = string.Empty;
        [Required] public string TimeZoneId { get; set; } = "Sri Lanka Standard Time";
        [DataType(DataType.Date)] public DateTime FiscalYearStart { get; set; } = new(DateTime.UtcNow.Year, 1, 1);
        [DataType(DataType.Date)] public DateTime FiscalYearEnd { get; set; } = new(DateTime.UtcNow.Year, 12, 31);
        [Range(0, 100)] public decimal VatRate { get; set; } = 18m;
        [Range(0, 100)] public decimal SsclRate { get; set; } = 2.5m;

        [Required] public string WarehouseCode { get; set; } = "WH-MAIN";
        [Required] public string WarehouseName { get; set; } = "Main Warehouse";
        public string? WarehouseLocation { get; set; }
        public string LocalStoragePath { get; set; } = string.Empty;

        [Required] public string AdminFullName { get; set; } = string.Empty;
        [Required, EmailAddress] public string AdminEmail { get; set; } = string.Empty;
        [Required, MinLength(12), DataType(DataType.Password)] public string AdminPassword { get; set; } = string.Empty;
        [Required, Compare(nameof(AdminPassword), ErrorMessage = "The SuperAdmin passwords do not match."), DataType(DataType.Password)]
        public string ConfirmAdminPassword { get; set; } = string.Empty;
    }
}
