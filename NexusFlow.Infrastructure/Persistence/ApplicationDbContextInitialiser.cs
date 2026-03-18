using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Entities.System;
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
                    new() { Key = "Inventory.AllowNegativeStock", Value = "False", DataType = "Boolean", Description = "Prevent selling items not in stock" },

                    // Sales Config
                    new() { Key = "Account.Sales.Receivable", Value = "1040", DataType = "Account", Description = "Trade Debtors" }, // Points to new account
                    new() { Key = "Account.Sales.Revenue", Value = "4010", DataType = "Account", Description = "Sales Income" },    // Existing
                    new() { Key = "Account.Tax.VATPayable", Value = "2050", DataType = "Account", Description = "VAT Payable" },    // Points to new account
                    new() { Key = "Account.Cost.COGS", Value = "5050", DataType = "Account", Description = "Cost of Goods Sold" },  // Points to new account

                    // Inventory Config
                    new() { Key = "Account.Inventory.RawMaterial", Value = "1031", DataType = "Account", Description = "RM Stock" }, // Specific!
                    new() { Key = "Account.Inventory.FinishedGood", Value = "1032", DataType = "Account", Description = "FG Stock" }, // Specific!
                    new() { Key = "Account.Liability.ServiceAccrual", Value = "2060", DataType = "Account", Description = "Services Payable" },

                    new() { Key = "Account.Liability.TradeCreditors", Value = "2010", DataType = "Account", Description = "Default Accounts Payable" }

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



            // ==========================================================
            // 4. SEED TAX CONFIGURATION
            // ==========================================================
            if (!await _context.TaxTypes.AnyAsync())
            {
                // 1. Fetch the correct liability accounts seeded via Migrations
                // Using the NEW Industry Standard Codes defined in our Enterprise COA
                // 2121: VAT Payable | 2122: SSCL Payable
                var vatAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "2121");
                var ssclAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "2122");

                // Architectural Guard Clause
                if (vatAccount == null || ssclAccount == null)
                {
                    throw new InvalidOperationException(
                        "CRITICAL ARCHITECTURE FAILURE: Tax Liability accounts (2121 or 2122) are missing from the Chart of Accounts. " +
                        "Ensure Entity Framework Migrations have been applied before running the seeder."
                    );
                }

                // 2. Create Tax Definitions linking directly to the GL
                var vat = new TaxType
                {
                    Name = "VAT",
                    Description = "Value Added Tax",
                    AccountId = vatAccount.Id
                };

                var sscl = new TaxType
                {
                    Name = "SSCL",
                    Description = "Social Security Levy",
                    AccountId = ssclAccount.Id
                };

                _context.TaxTypes.AddRange(vat, sscl);
                await _context.SaveChangesAsync(); // Save to generate IDs

                // 3. Set Historical Rates
                // Rule: Tax Engine uses "Effective Date" logic. We seed base rates into the past.
                _context.TaxRates.AddRange(
                    new TaxRate
                    {
                        TaxTypeId = vat.Id,
                        Rate = 18.00m,
                        EffectiveDate = DateTime.UtcNow.AddYears(-5) // Ensure it covers legacy data imports
                    },
                    new TaxRate
                    {
                        TaxTypeId = sscl.Id,
                        Rate = 2.50m,
                        EffectiveDate = DateTime.UtcNow.AddYears(-5)
                    }
                );

                await _context.SaveChangesAsync();
            }

            // ==========================================================
            // 5. SEED MASTER DATA (Product Attributes)
            // ==========================================================
            if (!_context.Brands.Any()) 
            {
                _context.Brands.AddRange(
                    new Brand { Name = "Emerald", Description = "Premium Shirts" },
                    new Brand { Name = "Signature", Description = "Formal Wear" }
                );
                await _context.SaveChangesAsync();
            }

            if (!_context.Categories.Any())
            {
                _context.Categories.AddRange(
                    new Category { Name = "Men's Shirts", Code = "MSH" },
                    new Category { Name = "Men's Trousers", Code = "MTR" }
                );
                await _context.SaveChangesAsync();
            }

            if (!_context.UnitOfMeasures.Any())
            {
                _context.UnitOfMeasures.AddRange(
                    new UnitOfMeasure { Name = "Piece", Symbol = "pcs" },
                    new UnitOfMeasure { Name = "Dozen", Symbol = "doz" }
                );
                await _context.SaveChangesAsync();
            }

            // 2. SEED RAW MATERIAL CATEGORIES
            // (Ensure you have a category for raw materials)
            var fabricCat = await _context.Categories.FirstOrDefaultAsync(c => c.Code == "RM-FABRIC");
            if (fabricCat == null)
            {
                fabricCat = new Category { Name = "Raw Materials - Fabric", Code = "RM-FABRIC" };
                _context.Categories.Add(fabricCat);
                await _context.SaveChangesAsync();
            }
        }


    }
}
