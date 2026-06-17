using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Features.System.DocumentTemplates
{
    public class DocumentTemplateDto
    {
        public int Id { get; set; }
        public DocumentType DocumentType { get; set; }
        public string DocumentTypeName => DocumentType.ToString();
        public string TemplateName { get; set; } = string.Empty;
        public TaxProfile TaxProfile { get; set; }
        public string TaxProfileName => TaxProfile.ToString();
        public string BlobUrl { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
    }
}
