using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Purchasing;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> builder)
        {
            // Unique Constraints
            // We cannot have two vendors with the same Tax ID (prevents duplicate vendor creation)
            builder.HasIndex(x => x.TaxRegNo).IsUnique();

            // Decimal Precision for Credit Limit
            builder.Property(x => x.CreditLimit)
                .HasColumnType("decimal(18,2)");

            // Relationships
            // (Assuming you have a GL Account entity)
            builder.HasOne<NexusFlow.Domain.Entities.Finance.Account>()
                .WithMany()
                .HasForeignKey(x => x.DefaultPayableAccountId)
                .OnDelete(DeleteBehavior.Restrict); // Don't delete Account if Vendor exists

            // Indexes for Search Performance
            builder.HasIndex(x => x.Name);
            builder.HasIndex(x => x.Email);
            builder.HasIndex(x => x.IsActive);
        }
    }
}
