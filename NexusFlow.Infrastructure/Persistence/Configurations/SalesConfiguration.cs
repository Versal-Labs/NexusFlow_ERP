using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Sales;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.CreditLimit).HasColumnType("decimal(18,2)");
        }
    }

    public class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
    {
        public void Configure(EntityTypeBuilder<SalesInvoice> builder)
        {
            builder.HasKey(x => x.Id);
            // Important: Invoice Number must be unique
            builder.HasIndex(x => x.InvoiceNumber).IsUnique();

            builder.Property(x => x.SubTotal).HasColumnType("decimal(18,2)");
            builder.Property(x => x.TotalTax).HasColumnType("decimal(18,2)");
            builder.Property(x => x.GrandTotal).HasColumnType("decimal(18,2)");
            builder.Property(x => x.TotalDiscount).HasColumnType("decimal(18,2)");
        }
    }
}
