using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using NexusFlow.Infrastructure;
using NexusFlow.Infrastructure.Services;
using NexusFlow.Infrastructure.Installation;
using NexusFlow.AppCore.Behaviors;
using NexusFlow.AppCore.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.IntegrationTests
{
    public class TestBase : IDisposable
    {
        protected IServiceScopeFactory _scopeFactory;
        protected ServiceProvider _serviceProvider;

        public TestBase()
        {
            var services = new ServiceCollection();

            string dbName = Guid.NewGuid().ToString();

            var configuration = new ConfigurationBuilder().Build();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(); // Satisfies ILogger dependencies
            services.AddMemoryCache();

            // 1. Mock the Database (Use In-Memory for speed)
            services.AddDbContext<ErpDbContext>(options =>
                options.UseInMemoryDatabase(dbName)
                    .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // 2. Register Interface Mappings
            services.AddScoped<IErpDbContext>(provider => provider.GetRequiredService<ErpDbContext>());
            services.AddScoped<ICurrentUserService, SystemCurrentUserService>();
            services.AddScoped<IStockService, StockService>();
            services.AddScoped<ITaxService, TaxService>();
            services.AddScoped<IJournalService, JournalService>();
            services.AddScoped<INumberSequenceService, NumberSequenceService>();
            services.AddScoped<IFinancialAccountResolver, FinancialAccountResolver>();
            services.AddScoped<IFinancialPeriodService, FinancialPeriodService>();
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(FinancialPeriodBehavior<,>));

            // 3. Register MediatR (Finds all your Handlers)
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IErpDbContext).Assembly));

            // 4. Build Provider
            _serviceProvider = services.BuildServiceProvider();
            _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

            // 5. SEED DATA
            SeedMasterData();
        }

        private void SeedMasterData()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

            // A. SYSTEM CONFIG (Critical for GL Linking)
            context.SystemConfigs.AddRange(
                new SystemConfig { Key = "Account.Inventory.RawMaterial", Value = "1031" },
                new SystemConfig { Key = "Account.Inventory.FinishedGood", Value = "1032" },
                new SystemConfig { Key = "Account.Inventory.WorkInProgress", Value = "1033" },
                new SystemConfig { Key = "Account.Liability.TradeCreditors", Value = "2010" },
                new SystemConfig { Key = "Account.Liability.ServiceAccrual", Value = "2060" },
                new SystemConfig { Key = "Account.Purchasing.UnbilledReceipts", Value = "2060" },
                new SystemConfig { Key = "Account.Asset.UndepositedFunds", Value = "1010" },
                new SystemConfig { Key = "Account.Asset.AccountsReceivable", Value = "1040" },
                new SystemConfig { Key = "Account.Liability.AccountsPayable", Value = "2010" },
                new SystemConfig { Key = "Account.Equity.OpeningBalance", Value = "3200" },
                new SystemConfig { Key = "Account.Sales.Revenue", Value = "4010" },
                new SystemConfig { Key = "Account.Sales.Receivable", Value = "1040" },
                new SystemConfig { Key = "Account.Tax.VATPayable", Value = "2050" },
                new SystemConfig { Key = "Account.Cost.COGS", Value = "5050" }
                ,new SystemConfig { Key = "Account.Inventory.Shrinkage", Value = "5060" }
                ,new SystemConfig { Key = "Account.Expense.PurchaseVariance", Value = "5061" }
                ,new SystemConfig { Key = "Production.OverproductionTolerancePercent", Value = "5" }
            );

            // B. ACCOUNTS (The Chart of Accounts)
            context.Accounts.AddRange(
                new Account { Id = 1031, Code = "1031", Name = "Stock - Raw Material", Type = AccountType.Asset, IsTransactionAccount = true },
                new Account { Id = 1032, Code = "1032", Name = "Stock - Finished Goods", Type = AccountType.Asset, IsTransactionAccount = true },
                new Account { Id = 1033, Code = "1033", Name = "Stock - Work in Progress", Type = AccountType.Asset, IsTransactionAccount = true },
                new Account { Id = 1010, Code = "1010", Name = "Cash in Hand", Type = AccountType.Asset, IsTransactionAccount = true },
                new Account { Id = 1020, Code = "1020", Name = "Bank - Sampath", Type = AccountType.Asset, IsTransactionAccount = true },
                new Account { Id = 2010, Code = "2010", Name = "Trade Creditors", Type = AccountType.Liability, IsTransactionAccount = true },
                new Account { Id = 2060, Code = "2060", Name = "Service Liability", Type = AccountType.Liability, IsTransactionAccount = true },
                new Account { Id = 4010, Code = "4010", Name = "Sales Revenue", Type = AccountType.Revenue, IsTransactionAccount = true },
                new Account { Id = 1040, Code = "1040", Name = "Receivables", Type = AccountType.Asset, IsTransactionAccount = true },
                new Account { Id = 2050, Code = "2050", Name = "VAT Payable", Type = AccountType.Liability, IsTransactionAccount = true },
                new Account { Id = 3200, Code = "3200", Name = "Opening Balance Equity", Type = AccountType.Equity, IsTransactionAccount = true },
                new Account { Id = 5050, Code = "5050", Name = "COGS", Type = AccountType.Expense, IsTransactionAccount = true }
                ,new Account { Id = 5060, Code = "5060", Name = "Production Loss", Type = AccountType.Expense, IsTransactionAccount = true }
                ,new Account { Id = 5061, Code = "5061", Name = "Purchase Variance", Type = AccountType.Expense, IsTransactionAccount = true }
            );

            // C. TAX
            var vatType = new TaxType { Name = "VAT", AccountId = 2050 };
            context.TaxTypes.Add(vatType);
            context.TaxRates.Add(new TaxRate { TaxType = vatType, Rate = 18.0m, EffectiveDate = DateTime.MinValue });

            // D. PRODUCTS (Fabric & Jeans)
            var brand = new Brand { Name = "Test Brand" };
            var unit = new UnitOfMeasure { Name = "Piece", Symbol = "pcs" };
            var rawMaterialCategory = new Category { Name = "Raw Material", Code = "RM", InventoryAccountId = 1031, CogsAccountId = 5050 };
            var finishedGoodCategory = new Category { Name = "Finished Good", Code = "FG", InventoryAccountId = 1032, CogsAccountId = 5050, SalesAccountId = 4010 };
            var fabricProduct = new Product { Name = "Fabric", Brand = brand, UnitOfMeasure = unit, Category = rawMaterialCategory, Type = ProductType.RawMaterial };
            var jeanProduct = new Product { Name = "Jean", Brand = brand, UnitOfMeasure = unit, Category = finishedGoodCategory, Type = ProductType.FinishedGood };
            var fabric = new ProductVariant { Id = 100, Product = fabricProduct, Name = "Fabric Roll", CostPrice = 0, SellingPrice = 0, SKU = "RM-001" };
            var jean = new ProductVariant { Id = 200, Product = jeanProduct, Name = "Blue Jean", CostPrice = 0, SellingPrice = 5000, SKU = "FG-001" };
            context.ProductVariants.AddRange(fabric, jean);

            // E. BOM (1 Jean = 1.5 Fabric)
            var bom = new BillOfMaterial { ProductVariantId = 200, IsActive = true, Name = "Standard Jean" };
            context.BillOfMaterials.Add(bom);
            context.BomComponents.Add(new BomComponent { BillOfMaterial = bom, MaterialVariantId = 100, Quantity = 1.5m });

            // F. WAREHOUSES & SUPPLIER & CUSTOMER
            context.Warehouses.AddRange(
                new Warehouse { Id = 1, Code = "MAIN", Name = "Main Store", Type = WarehouseType.Internal },
                new Warehouse { Id = 2, Code = "FACTORY", Name = "Factory", Type = WarehouseType.Subcontractor }
            );

            context.NumberSequences.AddRange(
                new NumberSequence { Module = "GRN", Prefix = "GRN", NextNumber = 1 },
                new NumberSequence { Module = "Purchasing", Prefix = "PO", NextNumber = 1 },
                new NumberSequence { Module = "SalesInvoice", Prefix = "INV", NextNumber = 1 },
                new NumberSequence { Module = "Payment", Prefix = "PAY", NextNumber = 1 },
                new NumberSequence { Module = "Receipt", Prefix = "REC", NextNumber = 1 },
                new NumberSequence { Module = "JOURNAL", Prefix = "JE", NextNumber = 1 },
                new NumberSequence { Module = "OpeningStock", Prefix = "OBSTK", NextNumber = 1 }
                ,new NumberSequence { Module = "ProductionOrder", Prefix = "PWO", NextNumber = 1 }
                ,new NumberSequence { Module = "MaterialIssue", Prefix = "MI", NextNumber = 1 }
                ,new NumberSequence { Module = "MaterialReturn", Prefix = "MR", NextNumber = 1 }
                ,new NumberSequence { Module = "ProductionReceipt", Prefix = "PRD", NextNumber = 1 }
                ,new NumberSequence { Module = "DebitNote", Prefix = "DN", NextNumber = 1 }
            );

            context.Suppliers.Add(new Domain.Entities.Purchasing.Supplier { Id = 1, Name = "Fabric Supplier Ltd", DefaultPayableAccountId = 2010 });
            context.Customers.Add(new Domain.Entities.Sales.Customer
            {
                Id = 1,
                Name = "Retail Shop",
                DefaultReceivableAccountId = 1040,
                DefaultRevenueAccountId = 4010
            });

            // G. OPEN FINANCIAL PERIOD
            context.FinancialPeriods.Add(new FinancialPeriod { Name = "2024", StartDate = DateTime.MinValue, EndDate = DateTime.MaxValue, IsClosed = false });

            context.SaveChanges();
        }

        // Helper to run commands
        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
        {
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            return await mediator.Send(request);
        }

        // Helper to query DB for assertions
        public async Task<TEntity?> FindAsync<TEntity>(object id) where TEntity : class
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            return await context.FindAsync<TEntity>(id);
        }

        public void Dispose() => _serviceProvider?.Dispose();
    }
}
