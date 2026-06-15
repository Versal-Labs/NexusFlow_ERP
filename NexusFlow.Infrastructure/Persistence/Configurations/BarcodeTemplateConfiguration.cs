using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Master;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class BarcodeTemplateConfiguration : IEntityTypeConfiguration<BarcodeTemplate>
    {
        public void Configure(EntityTypeBuilder<BarcodeTemplate> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .HasMaxLength(150)
                .IsRequired();

            builder.HasIndex(x => x.Name)
                .IsUnique();

            builder.HasIndex(x => x.IsDefault)
                .HasFilter("[IsDefault] = 1")
                .IsUnique();

            builder.Property(x => x.PageWidthMM).HasColumnType("decimal(9,3)");
            builder.Property(x => x.PageHeightMM).HasColumnType("decimal(9,3)");
            builder.Property(x => x.StickerWidthMM).HasColumnType("decimal(9,3)");
            builder.Property(x => x.StickerHeightMM).HasColumnType("decimal(9,3)");
            builder.Property(x => x.MarginTopMM).HasColumnType("decimal(9,3)");
            builder.Property(x => x.MarginLeftMM).HasColumnType("decimal(9,3)");
            builder.Property(x => x.HorizontalGapMM).HasColumnType("decimal(9,3)");
            builder.Property(x => x.VerticalGapMM).HasColumnType("decimal(9,3)");
        }
    }
}
