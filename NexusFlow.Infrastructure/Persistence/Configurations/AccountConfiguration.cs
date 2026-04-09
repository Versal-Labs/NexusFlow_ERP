using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence.Configurations
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            // 1. Identity Key
            // =========================================================
            // SCHEMA CONSTRAINTS
            // =========================================================
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();

            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

            // ARCHITECTURAL OVERRIDE: 
            // Precision must be 18,4 to prevent rounding drift in ledger math.
            builder.Property(x => x.Balance)
                   .HasPrecision(18, 4);

            // =========================================================
            // THE SELF-REFERENCING TREE
            // =========================================================
            builder.HasOne(x => x.ParentAccount)
                   .WithMany(x => x.ChildAccounts)
                   .HasForeignKey(x => x.ParentAccountId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Property(a => a.IsActive).HasDefaultValue(true);
            builder.Property(a => a.IsSystemAccount).HasDefaultValue(false);
            builder.Property(a => a.RequiresReconciliation).HasDefaultValue(false);

            // =========================================================
            // SEED DATA: INDUSTRY STANDARD COA
            // =========================================================

            // 1. Root Nodes (Preserving Existing IDs 1-5)
            builder.HasData(
                new Account { Id = 1, Code = "1000", Name = "Assets", Type = AccountType.Asset, IsTransactionAccount = false, ParentAccountId = null },
                new Account { Id = 2, Code = "2000", Name = "Liabilities", Type = AccountType.Liability, IsTransactionAccount = false, ParentAccountId = null },
                new Account { Id = 3, Code = "3000", Name = "Equity", Type = AccountType.Equity, IsTransactionAccount = false, ParentAccountId = null },
                new Account { Id = 4, Code = "4000", Name = "Revenue", Type = AccountType.Revenue, IsTransactionAccount = false, ParentAccountId = null },
                new Account { Id = 5, Code = "6000", Name = "Operating Expenses", Type = AccountType.Expense, IsTransactionAccount = false, ParentAccountId = null }
            );

            // 2. Intermediate Folders (New IDs starting from 100)
            builder.HasData(
                // Asset Folders
                new Account { Id = 101, Code = "1100", Name = "Current Assets", Type = AccountType.Asset, IsTransactionAccount = false, ParentAccountId = 1 },
                new Account { Id = 102, Code = "1110", Name = "Cash & Cash Equivalents", Type = AccountType.Asset, IsTransactionAccount = false, ParentAccountId = 101 },
                // Liability Folders
                new Account { Id = 201, Code = "2100", Name = "Current Liabilities", Type = AccountType.Liability, IsTransactionAccount = false, ParentAccountId = 2 },
                new Account { Id = 202, Code = "2120", Name = "Tax Payable", Type = AccountType.Liability, IsTransactionAccount = false, ParentAccountId = 201 },
                // Revenue Folders
                new Account { Id = 401, Code = "4100", Name = "Operating Revenue", Type = AccountType.Revenue, IsTransactionAccount = false, ParentAccountId = 4 },
                // COGS Root & Folders (New Root for Direct Costs)
                new Account { Id = 500, Code = "5000", Name = "Cost of Goods Sold", Type = AccountType.Expense, IsTransactionAccount = false, ParentAccountId = null },
                // Expense Folders
                new Account { Id = 601, Code = "6200", Name = "Administrative Expenses", Type = AccountType.Expense, IsTransactionAccount = false, ParentAccountId = 5 },
                new Account { Id = 602, Code = "6300", Name = "Sales & Marketing", Type = AccountType.Expense, IsTransactionAccount = false, ParentAccountId = 5 }
            );

            // 3. Modifying Your Existing Transactions (IDs 6-14)
            builder.HasData(
                new Account { Id = 6, Code = "1111", Name = "Cash in Hand", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = 102 },
                new Account { Id = 7, Code = "1112", Name = "Bank - Sampath", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = 102 },
                new Account { Id = 8, Code = "1130", Name = "Inventory Assets", Type = AccountType.Asset, IsTransactionAccount = false, ParentAccountId = 101 },
                new Account { Id = 9, Code = "4110", Name = "Sales Revenue - FG", Type = AccountType.Revenue, IsTransactionAccount = true, ParentAccountId = 401 },
                new Account { Id = 10, Code = "6210", Name = "Rent Expense", Type = AccountType.Expense, IsTransactionAccount = true, ParentAccountId = 601 },
                new Account { Id = 11, Code = "6220", Name = "Electricity & Utilities", Type = AccountType.Expense, IsTransactionAccount = true, ParentAccountId = 601 },
                new Account { Id = 12, Code = "6310", Name = "Advertising & Marketing", Type = AccountType.Expense, IsTransactionAccount = true, ParentAccountId = 602 },
                new Account { Id = 13, Code = "2121", Name = "VAT Payable", Type = AccountType.Liability, IsTransactionAccount = true, ParentAccountId = 202 },
                new Account { Id = 14, Code = "2122", Name = "SSCL Payable", Type = AccountType.Liability, IsTransactionAccount = true, ParentAccountId = 202 }
            );

            // 4. Injecting Missing Critical Nexus ERP Accounts
            builder.HasData(
                // Control Accounts (Mandatory for Invoices and Purchasing)
                new Account { Id = 1001, Code = "1120", Name = "Accounts Receivable (AR)", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = 101 },
                new Account { Id = 2001, Code = "2110", Name = "Accounts Payable (AP)", Type = AccountType.Liability, IsTransactionAccount = true, ParentAccountId = 201 },

                // Inventory Sub-Accounts (Mandatory for Strict FIFO Valuation rules)
                new Account { Id = 1002, Code = "1131", Name = "Raw Materials (RM) Inventory", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = 8 },
                new Account { Id = 1003, Code = "1132", Name = "Finished Goods (FG) Inventory", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = 8 },

                // Job Work & Manufacturing Costs (Mandatory for Light Manufacturing rule)
                new Account { Id = 5001, Code = "5110", Name = "Raw Material Consumption", Type = AccountType.Expense, IsTransactionAccount = true, ParentAccountId = 500 },
                new Account { Id = 5002, Code = "5120", Name = "Outsourced Job Work Costs", Type = AccountType.Expense, IsTransactionAccount = true, ParentAccountId = 500 },

                // Equity & Retained Earnings
                new Account { Id = 3001, Code = "3100", Name = "Share Capital", Type = AccountType.Equity, IsTransactionAccount = true, ParentAccountId = 3 },
                new Account { Id = 3002, Code = "3200", Name = "Retained Earnings", Type = AccountType.Equity, IsTransactionAccount = true, ParentAccountId = 3 }
            );
        }
    }
}
