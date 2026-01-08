using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;
using NexusFlow.Infrastructure.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence
{
    public class ApplicationDbContextInitialiser
    {
        private readonly ILogger<ApplicationDbContextInitialiser> _logger;
        private readonly ErpDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ApplicationDbContextInitialiser(
                            ILogger<ApplicationDbContextInitialiser> logger,
                            ErpDbContext context,
                            UserManager<ApplicationUser> userManager,
                            RoleManager<IdentityRole> roleManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task InitialiseAsync()
        {
            try
            {
                if (_context.Database.IsSqlServer())
                {
                    await _context.Database.MigrateAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing the database.");
                throw;
            }
        }

        public async Task SeedAsync()
        {
            try
            {
                await TrySeedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }

        public async Task TrySeedAsync()
        {
            // 1. Seed Roles
            var roles = new[] { "Admin", "Accountant", "StoreKeeper", "SalesRep" };

            foreach (var roleName in roles)
            {
                if (_roleManager.Roles.All(r => r.Name != roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed Default Admin User
            var administrator = new ApplicationUser
            {
                UserName = "admin@nexusflow.com",
                Email = "admin@nexusflow.com",
                FullName = "System Administrator",
                IsActive = true,
                EmailConfirmed = true
            };

            if (_userManager.Users.All(u => u.UserName != administrator.UserName))
            {
                await _userManager.CreateAsync(administrator, "Admin@123"); // Default Password
                await _userManager.AddToRolesAsync(administrator, new[] { "Admin" });
            }

            if (!_context.SystemConfigs.Any())
            {
                _context.SystemConfigs.AddRange(new List<SystemConfig>
                {
                    // Company Info
                    new() { Key = "Company.Name", Value = "Lanka Clothing Traders", DataType = "String", Description = "Name printed on documents" },
                    new() { Key = "Company.TaxRegNo", Value = "TIN-123456789", DataType = "String", Description = "Tax Identification Number" },
            
                    // Financial Defaults
                    new() { Key = "Finance.BaseCurrency", Value = "LKR", DataType = "String", Description = "System reporting currency" },
                    new() { Key = "Tax.VAT.Rate", Value = "18", DataType = "Decimal", Description = "Current VAT Percentage" },
                    new() { Key = "Tax.SSCL.Rate", Value = "2.5", DataType = "Decimal", Description = "Social Security Contribution Levy Rate" },
            
                    // Inventory Rules
                    new() { Key = "Inventory.AllowNegativeStock", Value = "False", DataType = "Boolean", Description = "Prevent selling items not in stock" }
                });

                await _context.SaveChangesAsync();
            }

            // ==========================================================
            // 2. SEED NUMBER SEQUENCES
            // ==========================================================
            if (!_context.NumberSequences.Any())
            {
                _context.NumberSequences.AddRange(new List<NumberSequence>
                {
                    // Format: INV-1001
                    new() { Module = "Sales", Prefix = "INV", NextNumber = 1001, Delimiter = "-", Suffix = "" },
            
                    // Format: PO/2024/0001
                    new() { Module = "Purchasing", Prefix = "PO", NextNumber = 1, Delimiter = "/", Suffix = "/2024" },
            
                    // Format: GRN-5000
                    new() { Module = "Inventory", Prefix = "GRN", NextNumber = 5000, Delimiter = "-", Suffix = "" },
            
                    // Format: PAY-001
                    new() { Module = "Finance", Prefix = "PAY", NextNumber = 1, Delimiter = "-", Suffix = "" }
                });

                await _context.SaveChangesAsync();
            }

            if (!_context.Accounts.Any())
            {
                // --- LEVEL 1: ROOT TYPES ---
                var assets = new Account { Code = "1000", Name = "Assets", Type = AccountType.Asset, IsTransactionAccount = false };
                var liabilities = new Account { Code = "2000", Name = "Liabilities", Type = AccountType.Liability, IsTransactionAccount = false };
                var equity = new Account { Code = "3000", Name = "Equity", Type = AccountType.Equity, IsTransactionAccount = false };
                var revenue = new Account { Code = "4000", Name = "Revenue", Type = AccountType.Revenue, IsTransactionAccount = false };
                var expenses = new Account { Code = "5000", Name = "Expenses", Type = AccountType.Expense, IsTransactionAccount = false };

                _context.Accounts.AddRange(assets, liabilities, equity, revenue, expenses);
                await _context.SaveChangesAsync(); // Save to get IDs

                // --- LEVEL 2: COMMON ACCOUNTS ---
                var accounts = new List<Account>
                {
                    // Assets
                    new() { Code = "1010", Name = "Cash in Hand", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = assets.Id },
                    new() { Code = "1020", Name = "Bank - Sampath", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = assets.Id },
                    new() { Code = "1030", Name = "Inventory Assets", Type = AccountType.Asset, IsTransactionAccount = false, ParentAccountId = assets.Id }, // Folder for Stock
        
                    // Revenue
                    new() { Code = "4010", Name = "Sales Income", Type = AccountType.Revenue, IsTransactionAccount = true, ParentAccountId = revenue.Id },
        
                    // Expenses
                    new() { Code = "5010", Name = "Shop Rent", Type = AccountType.Expense, IsTransactionAccount = true, ParentAccountId = expenses.Id },
                    new() { Code = "5020", Name = "Electricity", Type = AccountType.Expense, IsTransactionAccount = true, ParentAccountId = expenses.Id }
                };

                _context.Accounts.AddRange(accounts);
                await _context.SaveChangesAsync();
            }
        }


    }
}
