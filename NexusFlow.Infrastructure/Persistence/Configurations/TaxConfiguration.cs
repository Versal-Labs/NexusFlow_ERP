using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class TaxTypeConfiguration : IEntityTypeConfiguration<TaxType>
    {
        public void Configure(EntityTypeBuilder<TaxType> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(50).IsRequired();

            // Link to GL Account (Critical for accounting)
            builder.HasOne(x => x.Account)
                   .WithMany()
                   .HasForeignKey(x => x.AccountId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
    {
        public void Configure(EntityTypeBuilder<TaxRate> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Rate).HasColumnType("decimal(18,2)");

            builder.HasOne(x => x.TaxType)
                   .WithMany(x => x.Rates)
                   .HasForeignKey(x => x.TaxTypeId);
        }
    }
}
