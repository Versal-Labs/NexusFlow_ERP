using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class SystemLookupConfiguration : IEntityTypeConfiguration<SystemLookup>
    {
        public void Configure(EntityTypeBuilder<SystemLookup> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Value).HasMaxLength(200).IsRequired();

            // Critical Index: We almost always query by Type + Active status
            builder.HasIndex(x => new { x.Type, x.IsActive });

            // Uniqueness: Code must be unique within a Type
            builder.HasIndex(x => new { x.Type, x.Code }).IsUnique();
        }
    }
}
