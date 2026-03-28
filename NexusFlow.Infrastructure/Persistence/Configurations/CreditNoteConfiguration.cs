using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Sales;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
    {
        public void Configure(EntityTypeBuilder<CreditNote> builder)
        {
            builder.ToTable("CreditNotes", "Sales");

            // ARCHITECTURAL FIX: Prevent multiple cascade paths from Customer -> Invoice -> CN
            builder.HasOne(c => c.SalesInvoice)
                .WithMany()
                .HasForeignKey(c => c.SalesInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
