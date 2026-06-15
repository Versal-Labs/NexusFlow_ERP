using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Finance;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Balance).HasPrecision(18, 4);

            builder.HasOne(x => x.ParentAccount)
                .WithMany(x => x.ChildAccounts)
                .HasForeignKey(x => x.ParentAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(a => a.IsActive).HasDefaultValue(true);
            builder.Property(a => a.IsSystemAccount).HasDefaultValue(false);
            builder.Property(a => a.RequiresReconciliation).HasDefaultValue(false);

            // System accounts are owned by versioned installation templates.
        }
    }
}
