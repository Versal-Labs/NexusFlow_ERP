using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
            builder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            builder.HasMany(x => x.Lines).WithOne(x => x.JournalEntry).HasForeignKey(x => x.JournalEntryId);
        }
    }

    public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
    {
        public void Configure(EntityTypeBuilder<JournalLine> builder)
        {
            builder.Property(x => x.Debit).HasColumnType("decimal(18,2)");
            builder.Property(x => x.Credit).HasColumnType("decimal(18,2)");
            builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
