using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
    {
        public void Configure(EntityTypeBuilder<SystemConfig> builder)
        {
            builder.HasKey(x => x.Id); // Uses ID from AuditableEntity

            // "Key" must be unique so we don't have duplicate settings
            builder.HasIndex(x => x.Key).IsUnique();

            builder.Property(x => x.Key)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Value)
                .HasMaxLength(500); // Allow reasonable length
        }
    }
}
