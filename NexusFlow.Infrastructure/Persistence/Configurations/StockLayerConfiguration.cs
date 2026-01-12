using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Inventory;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class StockLayerConfiguration : IEntityTypeConfiguration<StockLayer>
    {
        public void Configure(EntityTypeBuilder<StockLayer> builder)
        {
            builder.HasKey(x => x.Id);

            // Performance Index: We frequently search for "Oldest stock for Product X in Warehouse Y"
            builder.HasIndex(x => new { x.ProductVariantId, x.WarehouseId, x.DateReceived });

            builder.Property(x => x.UnitCost).HasColumnType("decimal(18,4)");
            builder.Property(x => x.RemainingQty).HasColumnType("decimal(18,4)");
            builder.Property(x => x.InitialQty).HasColumnType("decimal(18,4)");
        }
    }

    public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
    {
        public void Configure(EntityTypeBuilder<StockTransaction> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Qty).HasColumnType("decimal(18,4)");
            builder.Property(x => x.UnitCost).HasColumnType("decimal(18,4)");
            builder.Property(x => x.TotalValue).HasColumnType("decimal(18,4)");
        }
    }
}
