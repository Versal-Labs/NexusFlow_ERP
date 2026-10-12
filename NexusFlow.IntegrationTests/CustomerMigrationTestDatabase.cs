using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore;
using NexusFlow.AppCore.Features.Finance.CustomerMigration;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Enums;
using NexusFlow.Infrastructure;
using NexusFlow.Infrastructure.Installation;
using NexusFlow.Infrastructure.Services;

namespace NexusFlow.IntegrationTests;

internal sealed class CustomerMigrationTestDatabase : IAsyncDisposable
{
    private readonly string name = "NexusFlow_CustomerMigration_Test_" + Guid.NewGuid().ToString("N");
    public string ConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={name};Integrated Security=true;TrustServerCertificate=true";
    public ErpDbContext Open() => new(new DbContextOptionsBuilder<ErpDbContext>().UseSqlServer(ConnectionString).Options, new SystemCurrentUserService());
    public void Configure(IServiceCollection services, bool addConfiguration = true)
    {
        if (addConfiguration) services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = ConnectionString }).Build());
        services.AddLogging(); services.AddMemoryCache();
        services.AddDbContext<ErpDbContext>(options => options.UseSqlServer(ConnectionString));
        services.AddScoped<IErpDbContext>(sp => sp.GetRequiredService<ErpDbContext>());
        services.AddScoped<ICurrentUserService, SystemCurrentUserService>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>(); services.AddScoped<IJournalService, JournalService>();
        services.AddScoped<IFinancialAccountResolver, FinancialAccountResolver>();
        services.AddScoped<ICustomerMigrationFileReader, CustomerMigrationFileReader>();
        services.AddAppCore();
    }
    public async Task Initialize()
    {
        await using var db = Open(); await db.Database.EnsureCreatedAsync();
        db.Accounts.AddRange(new Account { Code = "AR", Name = "Customer Control", Type = AccountType.Asset, IsTransactionAccount = true },
            new Account { Code = "SAFE", Name = "Cheques in Safe", Type = AccountType.Asset, IsTransactionAccount = true },
            new Account { Code = "BANK", Name = "Bank", Type = AccountType.Asset, IsTransactionAccount = true },
            new Account { Code = "OBE", Name = "Opening Balance Equity", Type = AccountType.Equity, IsTransactionAccount = true },
            new Account { Code = "REV", Name = "Revenue", Type = AccountType.Revenue, IsTransactionAccount = true },
            new Account { Code = "AP", Name = "Supplier Control", Type = AccountType.Liability, IsTransactionAccount = true });
        db.SystemConfigs.AddRange(new SystemConfig { Key = "Account.Asset.UndepositedFunds", Value = "SAFE" },
            new SystemConfig { Key = "Account.Equity.OpeningBalance", Value = "OBE" }, new SystemConfig { Key = CustomerMigrationService.EnableKey, Value = "true" });
        db.SystemLookups.AddRange(new SystemLookup { Type = "CustomerGroup", Code = "RETAIL", Value = "Retail" },
            new SystemLookup { Type = "PriceLevel", Code = "STD", Value = "Standard" }, new SystemLookup { Type = "PaymentTerm", Code = "30", Value = "30 Days" });
        db.Banks.Add(new Bank { BankCode = "7010", Name = "Test Bank", Branches = new List<BankBranch> { new() { BranchCode = "001", BranchName = "Test Branch" } } });
        db.FinancialPeriods.Add(new FinancialPeriod { Name = "Test 2026", StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) });
        foreach (var module in new[] { "JOURNAL", "Receipt", "Payment", "CustomerDebitMemo" }) db.NumberSequences.Add(new NumberSequence { Module = module, Prefix = module, NextNumber = 1 });
        await db.SaveChangesAsync();
        var ar = await db.Accounts.SingleAsync(x => x.Code == "AR"); var revenue = await db.Accounts.SingleAsync(x => x.Code == "REV");
        foreach (var code in new[] { "C001", "C002" }) db.Customers.Add(new Customer { LegacyCustomerCode = code, Name = "Test " + code, TaxRegNo = "N/A",
            ContactPerson = "Contact", Email = "test@example.com", Phone = "0771234567", AddressLine1 = "Test Road", Province = "Western", District = "Colombo", City = "Colombo",
            CustomerGroupId = await db.SystemLookups.Where(x => x.Type == "CustomerGroup").Select(x => x.Id).SingleAsync(),
            PriceLevelId = await db.SystemLookups.Where(x => x.Type == "PriceLevel").Select(x => x.Id).SingleAsync(),
            PaymentTermId = await db.SystemLookups.Where(x => x.Type == "PaymentTerm").Select(x => x.Id).SingleAsync(),
            DefaultReceivableAccountId = ar.Id, DefaultRevenueAccountId = revenue.Id });
        db.Suppliers.Add(new Supplier { Name = "Test Supplier", DefaultPayableAccountId = await db.Accounts.Where(x => x.Code == "AP").Select(x => x.Id).SingleAsync() });
        await db.SaveChangesAsync();
    }
    public async ValueTask DisposeAsync()
    {
        const string prefix = "NexusFlow_CustomerMigration_Test_";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _)) throw new InvalidOperationException("Invalid disposable test database name.");
        await using var db = Open(); await db.Database.EnsureDeletedAsync();
    }
}
