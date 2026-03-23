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

    public class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
    {
        public void Configure(EntityTypeBuilder<CommissionRule> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
            builder.Property(c => c.CommissionPercentage).HasPrecision(18, 4);

            // Optional Category Link
            builder.HasOne(c => c.Category)
                   .WithMany()
                   .HasForeignKey(c => c.CategoryId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Optional Employee Link (The Rep Override)
            builder.HasOne(c => c.Employee)
                   .WithMany()
                   .HasForeignKey(c => c.EmployeeId)
                   .OnDelete(DeleteBehavior.Cascade); // Safe to delete a rule if the employee is purged
        }
    }

    public class CommissionLedgerConfiguration : IEntityTypeConfiguration<CommissionLedger>
    {
        public void Configure(EntityTypeBuilder<CommissionLedger> builder)
        {
            builder.HasKey(c => c.Id);

            builder.HasOne(c => c.SalesRep)
                   .WithMany()
                   .HasForeignKey(c => c.SalesRepId)
                   .OnDelete(DeleteBehavior.Restrict); // Prevent deleting reps who are owed money

            builder.HasOne(c => c.SalesInvoice)
                   .WithMany()
                   .HasForeignKey(c => c.SalesInvoiceId)
                   .OnDelete(DeleteBehavior.Cascade); // If invoice is deleted/voided, erase unearned commission

            builder.Property(c => c.CommissionAmount).HasPrecision(18, 4);
        }
    }
}
