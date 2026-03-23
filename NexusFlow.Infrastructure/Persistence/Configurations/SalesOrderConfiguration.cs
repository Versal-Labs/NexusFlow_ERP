using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Sales;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
    {
        public void Configure(EntityTypeBuilder<SalesOrder> builder)
        {
            builder.HasKey(o => o.Id);
            builder.HasIndex(o => o.OrderNumber).IsUnique();
            builder.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();

            builder.HasOne(o => o.Customer)
                   .WithMany()
                   .HasForeignKey(o => o.CustomerId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(o => o.SalesRep)
                   .WithMany()
                   .HasForeignKey(o => o.SalesRepId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Precision for financials
            builder.Property(o => o.TotalAmount).HasPrecision(18, 4);
        }
    }

    public class SalesOrderItemConfiguration : IEntityTypeConfiguration<SalesOrderItem>
    {
        public void Configure(EntityTypeBuilder<SalesOrderItem> builder)
        {
            builder.HasKey(i => i.Id);

            builder.HasOne(i => i.SalesOrder)
                   .WithMany(o => o.Items)
                   .HasForeignKey(i => i.SalesOrderId)
                   .OnDelete(DeleteBehavior.Cascade); // Safe: Deleting an order deletes its items

            builder.HasOne(i => i.ProductVariant)
                   .WithMany()
                   .HasForeignKey(i => i.ProductVariantId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Property(i => i.Quantity).HasPrecision(18, 4);
            builder.Property(i => i.UnitPrice).HasPrecision(18, 4);
            builder.Property(i => i.Discount).HasPrecision(18, 4);
            builder.Property(i => i.LineTotal).HasPrecision(18, 4);
        }
    }
}
