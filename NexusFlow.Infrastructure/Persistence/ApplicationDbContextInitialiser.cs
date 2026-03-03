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

                    new() { Code = "2010", Name = "Trade Creditors (AP)", Type = AccountType.Liability, IsTransactionAccount = true, ParentAccountId = liabilities.Id },
        
                    // Revenue
                    new() { Code = "4010", Name = "Sales Income", Type = AccountType.Revenue, IsTransactionAccount = true, ParentAccountId = revenue.Id },
        
                    // Expenses
                    new() { Code = "5010", Name = "Shop Rent", Type = AccountType.Expense, IsTransactionAccount = true, ParentAccountId = expenses.Id },
                    new() { Code = "5020", Name = "Electricity", Type = AccountType.Expense, IsTransactionAccount = true, ParentAccountId = expenses.Id }
                };

                // --- LEVEL 3: TRANSACTION ACCOUNTS (The Missing Links) ---

                // 1. Get Parent IDs (We need the IDs generated by the DB)
                var inventoryFolderId = _context.Accounts.FirstOrDefault(a => a.Code == "1030")?.Id;
                var assetsId = _context.Accounts.FirstOrDefault(a => a.Code == "1000")?.Id;
                var liabilitiesId = _context.Accounts.FirstOrDefault(a => a.Code == "2000")?.Id;
                var expensesId = _context.Accounts.FirstOrDefault(a => a.Code == "5000")?.Id;

                if (inventoryFolderId != null && assetsId != null && liabilitiesId != null && expensesId != null)
                {
                    var newAccounts = new List<Account>();

                    // A. INVENTORY (Specific Buckets)
                    if (!_context.Accounts.Any(a => a.Code == "1031"))
                    {
                        // This is where we put Fabric/Buttons value
                        newAccounts.Add(new Account { Code = "1031", Name = "Stock - Raw Materials", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = inventoryFolderId });

                        // This is where we put Jeans value
                        newAccounts.Add(new Account { Code = "1032", Name = "Stock - Finished Goods", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = inventoryFolderId });
                    }

                    // B. RECEIVABLES (Who owes us money?)
                    if (!_context.Accounts.Any(a => a.Code == "1040"))
                    {
                        // Under Root Assets (or create a 'Current Assets' folder if you prefer)
                        newAccounts.Add(new Account { Code = "1040", Name = "Trade Debtors (Receivables)", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = assetsId });
                    }

                    // C. LIABILITIES (VAT & Services)
                    if (!_context.Accounts.Any(a => a.Code == "2050"))
                    {
                        // VAT Output (We collected it, we owe it to Govt)
                        newAccounts.Add(new Account { Code = "2050", Name = "VAT Payable", Type = AccountType.Liability, IsTransactionAccount = true, ParentAccountId = liabilitiesId });

                        // Subcontractor Liability
                        newAccounts.Add(new Account { Code = "2060", Name = "Service Accruals / AP", Type = AccountType.Liability, IsTransactionAccount = true, ParentAccountId = liabilitiesId });
                    }

                    // D. COST OF GOODS SOLD (Expense)
                    if (!_context.Accounts.Any(a => a.Code == "5050"))
                    {
                        // This is the specific expense account for Item Cost
                        newAccounts.Add(new Account { Code = "5050", Name = "Cost of Goods Sold (COGS)", Type = AccountType.Expense, IsTransactionAccount = true, ParentAccountId = expensesId });
                    }

                    if (newAccounts.Any())
                    {
                        _context.Accounts.AddRange(newAccounts);
                        await _context.SaveChangesAsync();
                    }
                }

                _context.Accounts.AddRange(accounts);
                await _context.SaveChangesAsync();
            }

            // ==========================================================
            // 4. SEED TAX CONFIGURATION
            // ==========================================================
            if (!_context.TaxTypes.Any())
            {
                // First, find or create the Liability Accounts for Taxes
                // Assuming "Liabilities" root is ID 2 (from previous seed)
                // We ideally should look them up by Code, but for seeding simplicity:

                // Let's ensure we have a "Duties & Taxes" parent folder
                var liabilitiesId = _context.Accounts.FirstOrDefault(a => a.Code == "2000")?.Id;

                if (liabilitiesId != null)
                {
                    var vatAccount = new Account { Code = "2050", Name = "VAT Payable", Type = AccountType.Liability, IsTransactionAccount = true, ParentAccountId = liabilitiesId };
                    var ssclAccount = new Account { Code = "2060", Name = "SSCL Payable", Type = AccountType.Liability, IsTransactionAccount = true, ParentAccountId = liabilitiesId };

                    _context.Accounts.AddRange(vatAccount, ssclAccount);
                    await _context.SaveChangesAsync();

                    // Now Create Tax Definitions
                    var vat = new TaxType { Name = "VAT", Description = "Value Added Tax", AccountId = vatAccount.Id };
                    var sscl = new TaxType { Name = "SSCL", Description = "Social Security Levy", AccountId = ssclAccount.Id };

                    _context.TaxTypes.AddRange(vat, sscl);
                    await _context.SaveChangesAsync();

                    // Set Rates
                    _context.TaxRates.AddRange(
                        new TaxRate { TaxTypeId = vat.Id, Rate = 18.00m, EffectiveDate = DateTime.UtcNow.AddYears(-1) },
                        new TaxRate { TaxTypeId = sscl.Id, Rate = 2.50m, EffectiveDate = DateTime.UtcNow.AddYears(-1) }
                    );

                    await _context.SaveChangesAsync();
                }
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
