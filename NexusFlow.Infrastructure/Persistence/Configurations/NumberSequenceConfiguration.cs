using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
    {
        public void Configure(EntityTypeBuilder<NumberSequence> builder)
        {
            builder.HasKey(x => x.Id);

            // Ensure we don't have two sequences for "Sales Invoice"
            builder.HasIndex(x => new { x.Module, x.Prefix }).IsUnique();

            builder.Property(x => x.Module).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Prefix).HasMaxLength(20).IsRequired();
            builder.Property(x => x.Delimiter).HasMaxLength(5);
        }
    }
}
