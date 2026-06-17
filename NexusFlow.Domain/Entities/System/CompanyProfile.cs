using NexusFlow.Domain.Common;

namespace NexusFlow.Domain.Entities.System
{
    public class CompanyProfile : AuditableEntity
    {
        public string? CompanyName { get; set; }
        public string? TaxRegistrationNumber { get; set; }
        public string? BusinessRegistrationNumber { get; set; }
        public string? PrimaryAddress { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? LogoBlobUrl { get; set; }
    }
}
