using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Sales;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class CreditNoteItemConfiguration : IEntityTypeConfiguration<CreditNoteItem>
    {
        public void Configure(EntityTypeBuilder<CreditNoteItem> builder)
        {
            builder.ToTable("CreditNoteItems", "Sales");

            // Deleting a CreditNote should delete its Items (This is a safe 1-to-many cascade)
            builder.HasOne(i => i.CreditNote)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.CreditNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            // ARCHITECTURAL FIX: Prevent cascade delete from ProductVariant
            builder.HasOne(i => i.ProductVariant)
                .WithMany()
                .HasForeignKey(i => i.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
