using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class BillOfMaterialConfiguration : IEntityTypeConfiguration<BillOfMaterial>
    {
        public void Configure(EntityTypeBuilder<BillOfMaterial> builder)
        {
            builder.HasKey(x => x.Id);

            // A variant can have multiple BOMs (e.g., Revision 1, Revision 2), but usually one active.
            builder.HasOne(x => x.ProductVariant)
                   .WithMany()
                   .HasForeignKey(x => x.ProductVariantId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class BomComponentConfiguration : IEntityTypeConfiguration<BomComponent>
    {
        public void Configure(EntityTypeBuilder<BomComponent> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Quantity).HasColumnType("decimal(18,4)"); // High precision for fabric

            builder.HasOne(x => x.MaterialVariant)
                   .WithMany()
                   .HasForeignKey(x => x.MaterialVariantId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
    {
        public void Configure(EntityTypeBuilder<Warehouse> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        }
    }
}
