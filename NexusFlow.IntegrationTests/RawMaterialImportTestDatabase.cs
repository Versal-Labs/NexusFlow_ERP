using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Behaviors;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Services;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using NexusFlow.Infrastructure;
using NexusFlow.Infrastructure.Installation;
using NexusFlow.Infrastructure.Services;

namespace NexusFlow.IntegrationTests;

// Isolated synthetic database only. Never reads application configuration or installation secrets.
internal sealed class RawMaterialImportTestDatabase : IAsyncDisposable
{
    private readonly string name = "NexusFlow_RawImport_Test_" + Guid.NewGuid().ToString("N");
    public string ConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={name};Integrated Security=true;TrustServerCertificate=true";
    public void Configure(IServiceCollection services, bool addConfiguration = true)
    {
        if (addConfiguration)
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = ConnectionString }).Build());
        services.AddLogging(); services.AddMemoryCache();
        services.AddDbContext<ErpDbContext>(options => options.UseSqlServer(ConnectionString));
        services.AddScoped<IErpDbContext>(sp => sp.GetRequiredService<ErpDbContext>());
        services.AddScoped<ICurrentUserService, SystemCurrentUserService>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<IJournalService, JournalService>();
        services.AddScoped<IFinancialPeriodService, FinancialPeriodService>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(FinancialPeriodBehavior<,>));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IErpDbContext).Assembly));
    }
    public ErpDbContext Open() => new(new DbContextOptionsBuilder<ErpDbContext>().UseSqlServer(ConnectionString).Options, new SystemCurrentUserService());
    public async Task Initialize()
    {
        await using var db = Open();
        await db.Database.EnsureCreatedAsync();
        db.Accounts.AddRange(
            new Account { Code = "1131", Name = "Raw Material Inventory", Type = AccountType.Asset, IsTransactionAccount = true },
            new Account { Code = "3200", Name = "Opening Balance Equity", Type = AccountType.Equity, IsTransactionAccount = true },
            new Account { Code = "5110", Name = "Raw Material Consumption", Type = AccountType.Expense, IsTransactionAccount = true },
            new Account { Code = "4110", Name = "Sales Revenue", Type = AccountType.Revenue, IsTransactionAccount = true });
        db.SystemConfigs.AddRange(
            new SystemConfig { Key = "Account.Inventory.RawMaterial", Value = "1131" },
            new SystemConfig { Key = "Account.Equity.OpeningBalance", Value = "3200" },
            new SystemConfig { Key = "Account.Cost.COGS", Value = "5110" },
            new SystemConfig { Key = "Account.Sales.Revenue", Value = "4110" });
        db.Brands.Add(new Brand { Name = "UNBRANDED" });
        db.UnitOfMeasures.Add(new UnitOfMeasure { Name = "Pieces", Symbol = "PCS" });
        db.Warehouses.Add(new Warehouse { Code = "WH-MAIN", Name = "Main Warehouse" });
        db.FinancialPeriods.Add(new FinancialPeriod { Name = "Test 2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), IsClosed = false });
        db.NumberSequences.AddRange(new NumberSequence { Module = "OpeningStock", Prefix = "OBSTK", NextNumber = 1 },
            new NumberSequence { Module = "JOURNAL", Prefix = "JE", NextNumber = 1 });
        await db.SaveChangesAsync();
    }
    public async ValueTask DisposeAsync()
    {
        // Exact GUID-based test database on LocalDB; never a configured user/application database.
        if (!name.StartsWith("NexusFlow_RawImport_Test_", StringComparison.Ordinal) || name.Length != 57)
            throw new InvalidOperationException("Unexpected test database name; refusing cleanup.");
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
    }
}
