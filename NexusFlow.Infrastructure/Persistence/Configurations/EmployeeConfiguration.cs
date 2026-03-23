using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.HR;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => e.EmployeeCode).IsUnique();
            builder.HasIndex(e => e.Email).IsUnique();

            builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            builder.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            builder.Property(e => e.Email).HasMaxLength(150).IsRequired();

            builder.HasOne(e => e.ApplicationUser)
                   .WithMany()
                   .HasForeignKey(e => e.ApplicationUserId)
                   .OnDelete(DeleteBehavior.SetNull); // If user is deleted, keep employee record for payroll history
        }
    }
}
