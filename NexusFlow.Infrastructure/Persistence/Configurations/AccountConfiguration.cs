using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            // 1. Identity Key
            builder.HasKey(x => x.Id);

            // 2. GL Code must be unique (Can't have two "1001" accounts)
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();

            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

            // 3. Decimal Precision for Money
            builder.Property(x => x.Balance)
                   .HasColumnType("decimal(18,2)");

            // 4. CONFIGURE THE SELF-REFERENCING TREE
            // An Account has One Parent, but a Parent has Many Children.
            // DeleteBehavior.Restrict prevents deleting a Parent if Children exist.
            builder.HasOne(x => x.ParentAccount)
                   .WithMany(x => x.ChildAccounts)
                   .HasForeignKey(x => x.ParentAccountId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
