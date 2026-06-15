using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.System;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public sealed class InstallationRecordConfiguration : IEntityTypeConfiguration<InstallationRecord>
    {
        public void Configure(EntityTypeBuilder<InstallationRecord> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.InstanceId).IsUnique();
            builder.Property(x => x.InstanceId).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ProductVersion).HasMaxLength(50).IsRequired();
            builder.Property(x => x.SchemaVersion).HasMaxLength(150).IsRequired();
            builder.Property(x => x.TemplateVersion).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        }
    }

    public sealed class AppliedInstallationStepConfiguration : IEntityTypeConfiguration<AppliedInstallationStep>
    {
        public void Configure(EntityTypeBuilder<AppliedInstallationStep> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.StepKey, x.StepVersion }).IsUnique();
            builder.Property(x => x.StepKey).HasMaxLength(100).IsRequired();
            builder.Property(x => x.StepVersion).HasMaxLength(50).IsRequired();
        }
    }
}
