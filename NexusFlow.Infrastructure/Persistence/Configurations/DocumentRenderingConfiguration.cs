using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.System;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
    {
        public void Configure(EntityTypeBuilder<CompanyProfile> builder)
        {
            builder.ToTable("CompanyProfiles", "System");

            builder.Property(x => x.CompanyName).HasMaxLength(200);
            builder.Property(x => x.TaxRegistrationNumber).HasMaxLength(100);
            builder.Property(x => x.BusinessRegistrationNumber).HasMaxLength(100);
            builder.Property(x => x.ContactEmail).HasMaxLength(200);
            builder.Property(x => x.ContactPhone).HasMaxLength(50);
            builder.Property(x => x.LogoBlobUrl).HasMaxLength(500);
        }
    }

    public class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
    {
        public void Configure(EntityTypeBuilder<DocumentTemplate> builder)
        {
            builder.ToTable("DocumentTemplates", "System");

            builder.Property(x => x.TemplateName)
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(x => x.BlobUrl)
                .HasMaxLength(500)
                .IsRequired();

            builder.HasIndex(x => new { x.DocumentType, x.TaxProfile, x.IsDefault })
                .HasDatabaseName("IX_DocumentTemplates_DefaultPerTypeTax")
                .HasFilter("[IsDefault] = 1")
                .IsUnique();
        }
    }
}
