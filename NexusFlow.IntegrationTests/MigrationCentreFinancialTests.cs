using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Features.Migration;
using NexusFlow.AppCore.Features.Treasury.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Services;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using NexusFlow.Infrastructure.Services;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.IntegrationTests;

public sealed partial class MigrationCentreTests
{
    [CustomerMigrationSqlFact]
    public async Task PostedStockDefaultWarehouse_CannotChangeSilently_WhenInventoryAccountIsShared()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "Stock");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); db.Accounts.Add(new() { Code = "INV", Name = "Inventory", Type = AccountType.Asset, IsTransactionAccount = true }); db.Brands.Add(new() { Name = "TEST" }); db.UnitOfMeasures.Add(new() { Name = "Metre", Symbol = "M" }); db.Warehouses.AddRange(new Warehouse { Code = "WH1", Name = "One" }, new Warehouse { Code = "WH2", Name = "Two" }); await db.SaveChangesAsync(default);
            db.Categories.Add(new() { Code = "RAW", Name = "Raw", InventoryAccountId = await db.Accounts.Where(x => x.Code == "INV").Select(x => (int?)x.Id).SingleAsync() }); await db.SaveChangesAsync(default);
            var s = MigrationJson.Read<MigrationSettings>((await db.MigrationProjects.SingleAsync()).SettingsJson); s.Mappings = new() { ["Brand"] = new() { ["TEST"] = await db.Brands.Select(x => x.Id).SingleAsync() }, ["Category"] = new() { ["RAW"] = await db.Categories.Select(x => x.Id).SingleAsync() } }; s.Defaults["StockUnit"] = await db.UnitOfMeasures.Select(x => x.Id).SingleAsync(); s.Defaults["StockWarehouse"] = await db.Warehouses.Where(x => x.Code == "WH1").Select(x => x.Id).SingleAsync(); await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().SaveSettingsAsync(id, s, default);
        }
        await Upload(provider, id, "Stock", "LegacyRecordId,Code,Name,ProductType,BrandCode,CategoryCode,Quantity,UnitCost,LayerDate\nL,FAB,Fabric,RawMaterial,TEST,RAW,2,1.125,2026-09-01\n"); await Post(provider, id, new() { Modules = ["Stock"] });
        using var check = provider.CreateScope(); var context = check.ServiceProvider.GetRequiredService<IErpDbContext>(); var service = check.ServiceProvider.GetRequiredService<MigrationCentreService>(); var settings = MigrationJson.Read<MigrationSettings>((await context.MigrationProjects.SingleAsync()).SettingsJson); settings.Defaults["StockWarehouse"] = await context.Warehouses.Where(x => x.Code == "WH2").Select(x => x.Id).SingleAsync(); await service.SaveSettingsAsync(id, settings, default);
        var preview = await service.ValidateAsync(id, new() { Modules = ["Stock"] }, default); preview.Units.SelectMany(x => x.Errors).Should().Contain(x => x.Message.Contains("posted scope has changed")); (await context.StockLayers.SingleAsync()).WarehouseId.Should().Be(await context.Warehouses.Where(x => x.Code == "WH1").Select(x => x.Id).SingleAsync());
    }
    [CustomerMigrationSqlFact]
    public async Task SparseMasters_KeepGenuineBlanksAndCodes_WithProductionDi()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "CustomerMaster", "SupplierMaster");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); db.SystemLookups.Add(new() { Type = "SupplierGroup", Code = "SUP", Value = "Suppliers" }); db.Accounts.Add(new() { Code = "EXP", Name = "Expense", Type = AccountType.Expense, IsTransactionAccount = true }); await db.SaveChangesAsync(default);
            var project = await db.MigrationProjects.SingleAsync(); var s = MigrationJson.Read<MigrationSettings>(project.SettingsJson);
            foreach (var pair in new[] { ("ReceivableAccount", "AR"), ("RevenueAccount", "REV"), ("PayableAccount", "AP"), ("ExpenseAccount", "EXP") }) s.Defaults[pair.Item1] = await db.Accounts.Where(x => x.Code == pair.Item2).Select(x => x.Id).SingleAsync();
            foreach (var type in new[] { "CustomerGroup", "SupplierGroup", "PriceLevel", "PaymentTerm" }) { var target = await db.SystemLookups.Where(x => x.Type == type).Select(x => x.Id).SingleAsync(); if (type == "PaymentTerm") { s.Defaults["CustomerPaymentTerm"] = target; s.Defaults["SupplierPaymentTerm"] = target; } else s.Defaults[type] = target; }
            await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().SaveSettingsAsync(id, s, default);
        }
        await Upload(provider, id, "CustomerMaster", "Code,Name\nN001,Sparse Customer\n"); await Upload(provider, id, "SupplierMaster", "Code,Name\nS001,Sparse Supplier\n"); await Post(provider, id, new() { Modules = ["CustomerMaster", "SupplierMaster"] });
        using var check = provider.CreateScope(); var context = check.ServiceProvider.GetRequiredService<IErpDbContext>(); var customer = await context.Customers.SingleAsync(x => x.LegacyCustomerCode == "N001"); var supplier = await context.Suppliers.SingleAsync(x => x.LegacySupplierCode == "S001");
        customer.Email.Should().BeEmpty(); customer.Phone.Should().BeEmpty(); customer.TaxRegNo.Should().BeEmpty(); customer.DetailsIncomplete.Should().BeTrue(); supplier.Email.Should().BeEmpty(); supplier.DetailsIncomplete.Should().BeTrue(); (await context.JournalEntries.CountAsync()).Should().Be(0);
        await Release(provider, id, "Customer", "N001"); await Release(provider, id, "Supplier", "S001");
    }
    [CustomerMigrationSqlFact]
    public async Task SupplierOpening_DueDatesCreditsPaymentsAndStatement_StayInSubledger()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "SupplierOpenings");
        using (var scope = provider.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); (await db.Suppliers.SingleAsync()).LegacySupplierCode = "S001"; await db.SaveChangesAsync(default); }
        await Upload(provider, id, "SupplierOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,DueDate,Amount\nB,S001,BILL-OLD,Bill,2026-09-01,2026-11-01,500\nC,S001,CREDIT-OLD,Credit,2026-09-02,,-50\n"); await Post(provider, id, new() { Modules = ["SupplierOpenings"] }); await Release(provider, id, "Supplier", "S001");
        using var scope2 = provider.CreateScope(); var context = scope2.ServiceProvider.GetRequiredService<IErpDbContext>(); var supplier = await context.Suppliers.SingleAsync(); var balance = scope2.ServiceProvider.GetRequiredService<SupplierBalanceService>(); var items = await context.SupplierOpeningItems.ToListAsync(); var bill = items.Single(x => x.Amount > 0); var credit = items.Single(x => x.Amount < 0);
        await balance.ApplyCreditAsync(credit.Id, bill.Id, null, 50, Opening, default); var snapshot = await balance.GetAsync(supplier.Id, Opening, default); snapshot.NetBalance.Should().Be(450); snapshot.Debts.Single().Balance.Should().Be(450); snapshot.Debts.Single().DueDate.Should().Be(new DateTime(2026, 11, 1));
        var handler = new RecordSupplierPaymentHandler(context, scope2.ServiceProvider.GetRequiredService<IJournalService>(), scope2.ServiceProvider.GetRequiredService<INumberSequenceService>(), scope2.ServiceProvider.GetRequiredService<IFinancialAccountResolver>());
        var result = await handler.Handle(new() { SupplierId = supplier.Id, PaymentAmount = 100, Date = Opening.AddDays(1), Method = PaymentMethod.Cash, AccountId = await context.Accounts.Where(x => x.Code == "BANK").Select(x => (int?)x.Id).SingleAsync(), Allocations = [new() { SupplierOpeningItemId = bill.Id, Amount = 100 }] }, default); result.Succeeded.Should().BeTrue(result.Message);
        (await balance.GetAsync(supplier.Id, Opening.AddDays(1), default)).NetBalance.Should().Be(350); (await balance.GetAsync(supplier.Id, Opening.AddDays(1), default)).Debts.Single().Balance.Should().Be(350);
        (await context.SupplierBills.CountAsync()).Should().Be(0); (await context.PaymentAllocations.SingleAsync()).SupplierOpeningItemId.Should().Be(bill.Id);
    }
    [CustomerMigrationSqlFact]
    public async Task FractionalStock_AllWarehousesAndOriginalFifoDates_ArePreserved()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "Stock");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); db.Accounts.Add(new() { Code = "INV", Name = "Inventory", Type = AccountType.Asset, IsTransactionAccount = true }); db.Brands.Add(new() { Name = "TEST" }); db.UnitOfMeasures.Add(new() { Name = "Metre", Symbol = "M" }); db.Warehouses.AddRange(new Warehouse { Code = "WH1", Name = "One" }, new Warehouse { Code = "WH2", Name = "Two" }); await db.SaveChangesAsync(default);
            db.Categories.Add(new() { Code = "RAW", Name = "Raw", InventoryAccountId = await db.Accounts.Where(x => x.Code == "INV").Select(x => (int?)x.Id).SingleAsync() }); await db.SaveChangesAsync(default);
            var s = MigrationJson.Read<MigrationSettings>((await db.MigrationProjects.SingleAsync()).SettingsJson); s.Mappings = new() { ["Brand"] = new() { ["TEST"] = await db.Brands.Select(x => x.Id).SingleAsync() }, ["Unit"] = new() { ["M"] = await db.UnitOfMeasures.Select(x => x.Id).SingleAsync() }, ["Category"] = new() { ["RAW"] = await db.Categories.Select(x => x.Id).SingleAsync() }, ["Warehouse"] = await db.Warehouses.ToDictionaryAsync(x => x.Code, x => x.Id) }; await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().SaveSettingsAsync(id, s, default);
        }
        await Upload(provider, id, "Stock", "LegacyRecordId,Code,Name,ProductType,BrandCode,CategoryCode,UnitCode,WarehouseCode,Quantity,UnitCost,LayerDate,Batch\nL2,FAB,Fabric,RawMaterial,TEST,RAW,M,WH1,2.125,1.234567,2026-09-02,SECOND\nL1,FAB,Fabric,RawMaterial,TEST,RAW,M,WH1,1.375,1.111111,2026-09-01,FIRST\nL3,FAB,Fabric,RawMaterial,TEST,RAW,M,WH2,0.5,1.5,2026-09-03,OTHER\n"); await Post(provider, id, new() { Modules = ["Stock"] }); await Release(provider, id, "Stock", "FAB");
        using var check = provider.CreateScope(); var context = check.ServiceProvider.GetRequiredService<IErpDbContext>(); var layers = await context.StockLayers.OrderBy(x => x.DateReceived).ToListAsync(); layers.Should().HaveCount(3); layers[0].UnitCost.Should().Be(1.111111m); layers[0].InitialQty.Should().Be(1.375m);
        var variant = await context.ProductVariants.SingleAsync(); using var tx = await context.BeginTransactionAsync(); var consumed = await new StockService(context).ConsumeStockAsync(variant.Id, layers[0].WarehouseId, 1.5m, "TEST-FIFO"); consumed.Succeeded.Should().BeTrue(); consumed.Data.Should().Be(1.68m); (await context.StockLayers.SingleAsync(x => x.Id == layers[0].Id)).RemainingQty.Should().Be(0); await tx.CommitAsync();
    }
    [CustomerMigrationSqlFact]
    public async Task Failure_RollsBackWholeParty_AndResumeSkipsCommittedCustomer()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); var failure = new MigrationFailureSwitch(); using var provider = Provider(database, services => services.AddScoped<IJournalService>(sp => new FailOnceMigrationJournal(new JournalService(sp.GetRequiredService<IErpDbContext>(), sp.GetRequiredService<INumberSequenceService>()), failure)));
        var id = await Create(provider, "CustomerOpenings"); await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,INV-A,Invoice,2026-09-01,1000\nB,C002,INV-B,Invoice,2026-09-02,500\n");
        var selection = new MigrationSelection { Modules = ["CustomerOpenings"] }; int run;
        using (var scope = provider.CreateScope()) { var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>(); var preview = await service.ValidateAsync(id, selection, default); await service.ApproveAsync(id, new(selection, preview.Hash, "Failure test"), default); run = await service.ExecuteAsync(id, selection, default); }
        using (var scope = provider.CreateScope()) { Func<Task> post = () => scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ExecuteRunAsync(run, default); await post.Should().ThrowAsync<MigrationRunException>(); }
        using (var scope = provider.CreateScope()) { var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>(); await service.MarkRunFailedAsync(run, "Synthetic failure", default); var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); (await db.CustomerOpeningItems.CountAsync()).Should().Be(1); (await db.MigrationUnits.CountAsync()).Should().Be(1); (await db.Accounts.SingleAsync(x => x.Code == "AR")).Balance.Should().Be(1000); await service.DispatchAsync(id, run, default); }
        using (var scope = provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ExecuteRunAsync(run, default);
        using var final = provider.CreateScope(); var context = final.ServiceProvider.GetRequiredService<IErpDbContext>(); (await context.CustomerOpeningItems.CountAsync()).Should().Be(2); (await context.JournalEntries.CountAsync()).Should().Be(2); (await context.MigrationRuns.SingleAsync()).Status.Should().Be("Completed"); (await context.Accounts.SingleAsync(x => x.Code == "AR")).Balance.Should().Be(1500);
    }
}
internal sealed class MigrationFailureSwitch { public int Failed; }
internal sealed class FailOnceMigrationJournal(IJournalService inner, MigrationFailureSwitch state) : IJournalService
{
    public Task<Result<int>> PostJournalAsync(JournalEntryRequest request)
    {
        if (request.Description.EndsWith("/C002") && Interlocked.Exchange(ref state.Failed, 1) == 0) return Task.FromResult(Result<int>.Failure("Synthetic journal failure"));
        return inner.PostJournalAsync(request);
    }
}
