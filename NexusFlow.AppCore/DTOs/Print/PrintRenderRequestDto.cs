using System;

namespace NexusFlow.AppCore.DTOs.Print
{
    public class PrintRenderRequestDto
    {
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string OutputAction { get; set; } = "Preview";
        public PrintOverridesDto Overrides { get; set; } = new();
    }

    public class PrintOverridesDto
    {
        public string? CustomerOrSupplierName { get; set; }
        public string? BillingAddress { get; set; }
        public string? ShippingAddress { get; set; }
        public string? Notes { get; set; }
    }

    public class GeneratedDocumentHistoryDto
    {
        public int Id { get; set; }
        public string OutputAction { get; set; } = string.Empty;
        public string Sha256Hash { get; set; } = string.Empty;
        public DateTime GeneratedAtUtc { get; set; }
        public string GeneratedByUserId { get; set; } = string.Empty;
        public bool HasOverrides { get; set; }
    }
}
