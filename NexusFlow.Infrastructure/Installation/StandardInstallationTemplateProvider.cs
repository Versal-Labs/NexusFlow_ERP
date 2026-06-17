using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Finance.Banks.Commands;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Domain.Enums;
using NexusFlow.Infrastructure.Data.Seeders;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class StandardInstallationTemplateProvider : IInstallationTemplateProvider
    {
        private const string PermissionClaimType = "Permission";
        private readonly ErpDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly InstallationRuntimeOptions _runtimeOptions;

        public StandardInstallationTemplateProvider(
            ErpDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            InstallationRuntimeOptions runtimeOptions)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
            _runtimeOptions = runtimeOptions;
        }

        public string TemplateVersion => "lk-light-manufacturing-1.0";

        public async Task ApplyAsync(InstallationRequest request, CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = 'NexusFlow.Installation', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000",
                cancellationToken);

            await EnsureAccountsAsync(cancellationToken);
            await EnsureSystemConfigsAsync(request, cancellationToken);
            await EnsureNumberSequencesAsync(cancellationToken);
            await EnsureTaxesAsync(request, cancellationToken);
            await EnsureLookupsAsync(cancellationToken);
            await EnsureOperationalDefaultsAsync(request, cancellationToken);
            await EnsureRolesAsync(cancellationToken);
            await EnsureInitialSuperAdminAsync(request, cancellationToken);
            await EnsureReferenceDataAsync(cancellationToken);
            await EnsureCompanyProfileAsync(request, cancellationToken);
            await EnsureInstallationRecordAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        private async Task EnsureCompanyProfileAsync(InstallationRequest request, CancellationToken cancellationToken)
        {
            if (!await _context.CompanyProfiles.AnyAsync(cancellationToken))
            {
                _context.CompanyProfiles.Add(new CompanyProfile
                {
                    CompanyName = request.CompanyName.Trim(),
                    TaxRegistrationNumber = request.TaxRegistrationNumber.Trim(),
                    ContactEmail = request.AdminEmail.Trim()
                });
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task EnsureAccountsAsync(CancellationToken cancellationToken)
        {
            var specifications = StandardAccounts();
            var existing = await _context.Accounts.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

            foreach (var specification in specifications)
            {
                if (!existing.TryGetValue(specification.Code, out var account))
                {
                    account = new Account
                    {
                        Code = specification.Code,
                        Name = specification.Name,
                        Type = specification.Type,
                        IsTransactionAccount = specification.IsTransaction,
                        IsSystemAccount = true,
                        IsActive = true
                    };
                    _context.Accounts.Add(account);
                    existing[specification.Code] = account;
                }
                else
                {
                    account.IsSystemAccount = true;
                    account.IsActive = true;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            foreach (var specification in specifications.Where(x => x.ParentCode != null))
            {
                existing[specification.Code].ParentAccountId = existing[specification.ParentCode!].Id;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureSystemConfigsAsync(InstallationRequest request, CancellationToken cancellationToken)
        {
            var mappings = new Dictionary<string, string>
            {
                [ConfigurationKeys.CompanyName] = request.CompanyName.Trim(),
                [ConfigurationKeys.CompanyTaxRegistrationNumber] = request.TaxRegistrationNumber.Trim(),
                [ConfigurationKeys.CompanyCanonicalUrl] = request.CanonicalUrl.Trim(),
                [ConfigurationKeys.CompanyTimeZone] = request.TimeZoneId,
                [ConfigurationKeys.FinanceBaseCurrency] = "LKR",
                [ConfigurationKeys.InventoryAllowNegativeStock] = "False",
                [ConfigurationKeys.StorageLocalPath] = request.LocalStoragePath,
                [AccountMappingKeys.AccountsReceivable] = "1120",
                [AccountMappingKeys.AccountsPayable] = "2110",
                [AccountMappingKeys.UndepositedFunds] = "1113",
                [AccountMappingKeys.OpeningBalanceEquity] = "3200",
                [AccountMappingKeys.SalesRevenue] = "4110",
                [AccountMappingKeys.VatPayable] = "2121",
                [AccountMappingKeys.VatReceivable] = "1140",
                [AccountMappingKeys.CostOfGoodsSold] = "5110",
                [AccountMappingKeys.RawMaterialInventory] = "1131",
                [AccountMappingKeys.FinishedGoodInventory] = "1132",
                [AccountMappingKeys.WorkInProgressInventory] = "1133",
                [AccountMappingKeys.InventoryShrinkage] = "6211",
                [AccountMappingKeys.InventorySurplus] = "4190",
                [AccountMappingKeys.ServiceAccrual] = "2130",
                [AccountMappingKeys.UnbilledReceipts] = "2140",
                [AccountMappingKeys.PurchaseVariance] = "6212",
                [AccountMappingKeys.BankFees] = "6213",
                [AccountMappingKeys.InterestIncome] = "4191",
                [AccountMappingKeys.BasicSalaryExpense] = "6100",
                [AccountMappingKeys.EmployerEpfExpense] = "6110",
                [AccountMappingKeys.EmployerEtfExpense] = "6120",
                [AccountMappingKeys.EpfPayable] = "2150",
                [AccountMappingKeys.EtfPayable] = "2151",
                [AccountMappingKeys.NetSalariesPayable] = "2152",
                [AccountMappingKeys.EmployeeLoansReceivable] = "1150",
                [AccountMappingKeys.SalaryAdvancesReceivable] = "1151",
                [AccountMappingKeys.CommissionExpense] = "6130",
                [AccountMappingKeys.AllowanceExpense] = "6140"
            };

            var existing = await _context.SystemConfigs.ToDictionaryAsync(x => x.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);
            foreach (var mapping in mappings)
            {
                if (!existing.TryGetValue(mapping.Key, out var config))
                {
                    config = new SystemConfig
                    {
                        Key = mapping.Key,
                        DataType = mapping.Key.StartsWith("Account.", StringComparison.OrdinalIgnoreCase) ? "Account" : "String",
                        Description = $"Required NexusFlow setting: {mapping.Key}"
                    };
                    _context.SystemConfigs.Add(config);
                }

                if (string.IsNullOrWhiteSpace(config.Value) ||
                    mapping.Key.StartsWith("Company.", StringComparison.OrdinalIgnoreCase) ||
                    mapping.Key == ConfigurationKeys.StorageLocalPath)
                {
                    config.Value = mapping.Value;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureNumberSequencesAsync(CancellationToken cancellationToken)
        {
            var definitions = new Dictionary<string, string>
            {
                [NumberSequenceKeys.CreditNote] = "CN", [NumberSequenceKeys.DebitNote] = "DN",
                [NumberSequenceKeys.Employee] = "EMP", [NumberSequenceKeys.GoodsReceipt] = "GRN",
                [NumberSequenceKeys.Journal] = "JE", [NumberSequenceKeys.MaterialIssue] = "MI",
                [NumberSequenceKeys.SalesOrder] = "SO", [NumberSequenceKeys.Payment] = "PAY",
                [NumberSequenceKeys.ProductionReceipt] = "PRD", [NumberSequenceKeys.Purchasing] = "PO",
                [NumberSequenceKeys.Receipt] = "REC", [NumberSequenceKeys.SalesInvoice] = "INV",
                [NumberSequenceKeys.StockAdjustment] = "ADJ", [NumberSequenceKeys.StockTake] = "ST",
                [NumberSequenceKeys.StockTransfer] = "TRF", [NumberSequenceKeys.SupplierBill] = "BILL"
            };

            var existing = await _context.NumberSequences.ToDictionaryAsync(x => x.Module, StringComparer.OrdinalIgnoreCase, cancellationToken);
            foreach (var definition in definitions)
            {
                if (!existing.ContainsKey(definition.Key))
                {
                    _context.NumberSequences.Add(new NumberSequence
                    {
                        Module = definition.Key,
                        Prefix = definition.Value,
                        NextNumber = 1,
                        Delimiter = "-",
                        Suffix = string.Empty
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureTaxesAsync(InstallationRequest request, CancellationToken cancellationToken)
        {
            var accounts = await _context.Accounts
                .Where(x => x.Code == "2121" || x.Code == "2122")
                .ToDictionaryAsync(x => x.Code, cancellationToken);

            await EnsureTaxAsync("VAT", "Value Added Tax", accounts["2121"].Id, request.VatRate, cancellationToken);
            await EnsureTaxAsync("SSCL", "Social Security Contribution Levy", accounts["2122"].Id, request.SsclRate, cancellationToken);
        }

        private async Task EnsureTaxAsync(
            string name,
            string description,
            int accountId,
            decimal rate,
            CancellationToken cancellationToken)
        {
            var tax = await _context.TaxTypes.Include(x => x.Rates)
                .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
            if (tax == null)
            {
                tax = new TaxType { Name = name, Description = description, AccountId = accountId };
                _context.TaxTypes.Add(tax);
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (!tax.Rates.Any())
            {
                _context.TaxRates.Add(new TaxRate
                {
                    TaxTypeId = tax.Id,
                    Rate = rate,
                    EffectiveDate = DateTime.UtcNow.Date
                });
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task EnsureLookupsAsync(CancellationToken cancellationToken)
        {
            var definitions = new[]
            {
                ("PaymentMethod", "CASH", "Cash", 10),
                ("PaymentMethod", "BANK", "Bank Transfer", 20),
                ("PaymentMethod", "CHEQUE", "Cheque", 30),
                ("PaymentTerm", "COD", "Cash on Delivery", 10),
                ("PaymentTerm", "NET30", "30 Days", 20)
            };

            foreach (var definition in definitions)
            {
                if (!await _context.SystemLookups.AnyAsync(
                    x => x.Type == definition.Item1 && x.Code == definition.Item2, cancellationToken))
                {
                    _context.SystemLookups.Add(new SystemLookup
                    {
                        Type = definition.Item1,
                        Code = definition.Item2,
                        Value = definition.Item3,
                        SortOrder = definition.Item4,
                        IsActive = true
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureOperationalDefaultsAsync(InstallationRequest request, CancellationToken cancellationToken)
        {
            if (!await _context.Warehouses.AnyAsync(cancellationToken))
            {
                _context.Warehouses.Add(new Warehouse
                {
                    Code = request.WarehouseCode.Trim().ToUpperInvariant(),
                    Name = request.WarehouseName.Trim(),
                    Location = request.WarehouseLocation.Trim(),
                    ManagerName = request.AdminFullName.Trim(),
                    Type = WarehouseType.Internal,
                    IsActive = true
                });
            }

            if (!await _context.FinancialPeriods.AnyAsync(x => !x.IsClosed, cancellationToken))
            {
                _context.FinancialPeriods.Add(new FinancialPeriod
                {
                    Name = $"{request.FiscalYearStart:yyyy} Financial Year",
                    Year = request.FiscalYearStart.Year,
                    Month = 0,
                    StartDate = request.FiscalYearStart.Date,
                    EndDate = request.FiscalYearEnd.Date,
                    IsClosed = false
                });
            }

            if (_runtimeOptions.StorageMode is StorageMode.Local or StorageMode.Hybrid)
            {
                Directory.CreateDirectory(request.LocalStoragePath);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureRolesAsync(CancellationToken cancellationToken)
        {
            foreach (var roleDefinition in DefaultRoleManifest.Roles)
            {
                var role = await _roleManager.FindByNameAsync(roleDefinition.Key);
                if (role == null)
                {
                    role = new IdentityRole(roleDefinition.Key);
                    var created = await _roleManager.CreateAsync(role);
                    if (!created.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join(", ", created.Errors.Select(x => x.Description)));
                    }
                }

                var existingClaims = await _roleManager.GetClaimsAsync(role);
                foreach (var permission in roleDefinition.Value)
                {
                    if (!existingClaims.Any(x => x.Type == PermissionClaimType && x.Value == permission))
                    {
                        var result = await _roleManager.AddClaimAsync(role, new Claim(PermissionClaimType, permission));
                        if (!result.Succeeded)
                        {
                            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Description)));
                        }
                    }
                }
            }
        }

        private async Task EnsureInitialSuperAdminAsync(InstallationRequest request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByEmailAsync(request.AdminEmail);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = request.AdminEmail.Trim(),
                    Email = request.AdminEmail.Trim(),
                    FullName = request.AdminFullName.Trim(),
                    EmailConfirmed = true,
                    IsActive = true
                };

                var created = await _userManager.CreateAsync(user, request.AdminPassword);
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(string.Join(", ", created.Errors.Select(x => x.Description)));
                }
            }

            if (!await _userManager.IsInRoleAsync(user, DefaultRoleManifest.SuperAdmin))
            {
                var assigned = await _userManager.AddToRoleAsync(user, DefaultRoleManifest.SuperAdmin);
                if (!assigned.Succeeded)
                {
                    throw new InvalidOperationException(string.Join(", ", assigned.Errors.Select(x => x.Description)));
                }
            }
        }

        private async Task EnsureReferenceDataAsync(CancellationToken cancellationToken)
        {
            var cityFile = Path.Combine(AppContext.BaseDirectory, "SeedData", "sri_lanka_cities.json");
            if (File.Exists(cityFile))
            {
                await LocationSeeder.SeedAsync(_context, cityFile);
            }

            var bankFile = Path.Combine(AppContext.BaseDirectory, "wwwroot", "data", "sri_lanka_banks.json");
            if (File.Exists(bankFile) && !await _context.Banks.AnyAsync(cancellationToken))
            {
                var handler = new SeedBanksHandler(_context);
                var result = await handler.Handle(new SeedBanksCommand { JsonFilePath = bankFile }, cancellationToken);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(result.Message);
                }
            }
        }

        private async Task EnsureInstallationRecordAsync(CancellationToken cancellationToken)
        {
            var instanceId = Environment.GetEnvironmentVariable("NEXUSFLOW_INSTANCE_ID") ?? "default";
            var record = await _context.InstallationRecords.FirstOrDefaultAsync(x => x.InstanceId == instanceId, cancellationToken);
            if (record == null)
            {
                _context.InstallationRecords.Add(new InstallationRecord
                {
                    InstanceId = instanceId,
                    ProductVersion = "1.0.0",
                    SchemaVersion = (await _context.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault() ?? "unknown",
                    TemplateVersion = TemplateVersion,
                    Status = "Installing"
                });
            }

            if (!await _context.AppliedInstallationSteps.AnyAsync(
                x => x.StepKey == "standard-template" && x.StepVersion == TemplateVersion, cancellationToken))
            {
                _context.AppliedInstallationSteps.Add(new AppliedInstallationStep
                {
                    StepKey = "standard-template",
                    StepVersion = TemplateVersion,
                    AppliedAtUtc = DateTimeOffset.UtcNow
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private static IReadOnlyList<AccountSpec> StandardAccounts() =>
        [
            new("1000", "Assets", AccountType.Asset, false),
            new("1100", "Current Assets", AccountType.Asset, false, "1000"),
            new("1110", "Cash and Cash Equivalents", AccountType.Asset, false, "1100"),
            new("1111", "Cash in Hand", AccountType.Asset, true, "1110"),
            new("1112", "Primary Bank", AccountType.Asset, true, "1110"),
            new("1113", "Undeposited Funds", AccountType.Asset, true, "1110"),
            new("1120", "Accounts Receivable", AccountType.Asset, true, "1100"),
            new("1130", "Inventory Assets", AccountType.Asset, false, "1100"),
            new("1131", "Raw Material Inventory", AccountType.Asset, true, "1130"),
            new("1132", "Finished Good Inventory", AccountType.Asset, true, "1130"),
            new("1133", "Work in Progress Inventory", AccountType.Asset, true, "1130"),
            new("1140", "Input VAT Receivable", AccountType.Asset, true, "1100"),
            new("1150", "Employee Loans Receivable", AccountType.Asset, true, "1100"),
            new("1151", "Salary Advances Receivable", AccountType.Asset, true, "1100"),
            new("2000", "Liabilities", AccountType.Liability, false),
            new("2100", "Current Liabilities", AccountType.Liability, false, "2000"),
            new("2110", "Accounts Payable", AccountType.Liability, true, "2100"),
            new("2120", "Tax Payable", AccountType.Liability, false, "2100"),
            new("2121", "VAT Payable", AccountType.Liability, true, "2120"),
            new("2122", "SSCL Payable", AccountType.Liability, true, "2120"),
            new("2130", "Service Accrual", AccountType.Liability, true, "2100"),
            new("2140", "Unbilled Receipts", AccountType.Liability, true, "2100"),
            new("2150", "EPF Payable", AccountType.Liability, true, "2100"),
            new("2151", "ETF Payable", AccountType.Liability, true, "2100"),
            new("2152", "Net Salaries Payable", AccountType.Liability, true, "2100"),
            new("3000", "Equity", AccountType.Equity, false),
            new("3100", "Share Capital", AccountType.Equity, true, "3000"),
            new("3200", "Opening Balance and Retained Earnings", AccountType.Equity, true, "3000"),
            new("4000", "Revenue", AccountType.Revenue, false),
            new("4110", "Sales Revenue", AccountType.Revenue, true, "4000"),
            new("4190", "Inventory Surplus", AccountType.Revenue, true, "4000"),
            new("4191", "Interest Income", AccountType.Revenue, true, "4000"),
            new("5000", "Cost of Goods Sold", AccountType.Expense, false),
            new("5110", "Cost of Goods Sold", AccountType.Expense, true, "5000"),
            new("6000", "Operating Expenses", AccountType.Expense, false),
            new("6100", "Basic Salary Expense", AccountType.Expense, true, "6000"),
            new("6110", "Employer EPF Expense", AccountType.Expense, true, "6000"),
            new("6120", "Employer ETF Expense", AccountType.Expense, true, "6000"),
            new("6130", "Commission Expense", AccountType.Expense, true, "6000"),
            new("6140", "Allowance Expense", AccountType.Expense, true, "6000"),
            new("6211", "Inventory Shrinkage", AccountType.Expense, true, "6000"),
            new("6212", "Purchase Variance", AccountType.Expense, true, "6000"),
            new("6213", "Bank Fees", AccountType.Expense, true, "6000")
        ];

        private sealed record AccountSpec(
            string Code,
            string Name,
            AccountType Type,
            bool IsTransaction,
            string? ParentCode = null);
    }
}
