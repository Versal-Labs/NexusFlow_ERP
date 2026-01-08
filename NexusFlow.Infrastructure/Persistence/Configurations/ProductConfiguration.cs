using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();

            // Relationships
            builder.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId);
            builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
            builder.HasOne(x => x.UnitOfMeasure).WithMany().HasForeignKey(x => x.UnitOfMeasureId);
        }
    }

    public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
    {
        public void Configure(EntityTypeBuilder<ProductVariant> builder)
        {
            builder.HasKey(x => x.Id);

            // Critical: SKU must be unique across the entire system
            builder.HasIndex(x => x.SKU).IsUnique();
            builder.Property(x => x.SKU).HasMaxLength(50).IsRequired();

            builder.Property(x => x.SellingPrice).HasColumnType("decimal(18,2)");
            builder.Property(x => x.CostPrice).HasColumnType("decimal(18,2)");

            builder.HasOne(x => x.Product)
                   .WithMany(x => x.Variants)
                   .HasForeignKey(x => x.ProductId)
                   .OnDelete(DeleteBehavior.Cascade); // Deleting Parent deletes variants (usually safe for setup, dangerous if history exists)
        }
    }
}
