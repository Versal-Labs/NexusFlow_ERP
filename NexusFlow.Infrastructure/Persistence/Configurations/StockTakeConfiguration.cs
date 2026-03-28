using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Inventory;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class StockTakeConfiguration : IEntityTypeConfiguration<StockTake>
    {
        public void Configure(EntityTypeBuilder<StockTake> builder)
        {
            builder.ToTable("StockTakes", "Inventory");

            builder.HasOne(s => s.Warehouse)
                .WithMany()
                .HasForeignKey(s => s.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class StockTakeItemConfiguration : IEntityTypeConfiguration<StockTakeItem>
    {
        public void Configure(EntityTypeBuilder<StockTakeItem> builder)
        {
            builder.ToTable("StockTakeItems", "Inventory");

            builder.HasOne(i => i.StockTake)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.StockTakeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(i => i.ProductVariant)
                .WithMany()
                .HasForeignKey(i => i.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
