using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Features.Migration;
using NexusFlow.AppCore.Features.Treasury.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Services;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Enums;

namespace NexusFlow.IntegrationTests;

public sealed partial class MigrationCentreTests
{
    [CustomerMigrationSqlFact]
    public async Task Cheques_SeparatePartyBalances_FromBankBookAndPendingClearing()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database);
        var id = await Create(provider, "CustomerOpenings", "SupplierOpenings", "ReceivedCheques", "IssuedCheques", "BankOpening");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); (await db.Suppliers.SingleAsync()).LegacySupplierCode = "S001";
            var settings = MigrationJson.Read<MigrationSettings>((await db.MigrationProjects.SingleAsync()).SettingsJson);
            settings.Mappings["BankBranch"] = new() { ["7010-001"] = await db.BankBranches.Select(x => x.Id).SingleAsync() };
            await db.SaveChangesAsync(default); await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().SaveSettingsAsync(id, settings, default);
        }
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nAR,C001,OLD-INV,Invoice,2026-09-01,1000\n");
        await Upload(provider, id, "SupplierOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,DueDate,Amount\nAP,S001,OLD-BILL,Bill,2026-09-01,2026-10-01,500\n");
        // Two instruments share one receipt reference. Their stable record/cheque identities remain distinct.
        await Upload(provider, id, "ReceivedCheques", "LegacyRecordId,Code,Reference,ChequeNumber,ChequeDate,ReceivedDate,BankBranchCode,Amount,Status,PartyReflected,BankReflected,BankAccountCode\nSAFE,C001,R-SAFE,0001,2026-10-20,2026-09-25,7010-001,200,InSafe,true,false,\nDEP1,C001,R-DEP,0002,2026-10-01,2026-09-25,7010-001,300,Deposited,true,true,BANK\nDEP2,C001,R-DEP,0003,2026-10-01,2026-09-25,7010-001,50,Deposited,true,true,BANK\n");
        await Upload(provider, id, "IssuedCheques", "LegacyRecordId,Code,Reference,ChequeNumber,ChequeDate,DocumentDate,Amount,BankAccountCode,PartyReflected,BankReflected\nISS,S001,P-OLD,0004,2026-10-01,2026-09-25,100,BANK,true,true\n");
        await Upload(provider, id, "BankOpening", "Code,Amount,StatementBalance\nBANK,1000,750\n");
        await Post(provider, id, new() { Modules = ["CustomerOpenings", "SupplierOpenings", "ReceivedCheques", "IssuedCheques", "BankOpening"] });
        await Release(provider, id, "Customer", "C001"); await Release(provider, id, "Supplier", "S001"); await Release(provider, id, "Bank", "BANK");
        using var check = provider.CreateScope(); var context = check.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await context.Accounts.SingleAsync(x => x.Code == "AR")).Balance.Should().Be(1000);
        (await context.Accounts.SingleAsync(x => x.Code == "AP")).Balance.Should().Be(-500);
        (await context.Accounts.SingleAsync(x => x.Code == "SAFE")).Balance.Should().Be(200);
        (await context.Accounts.SingleAsync(x => x.Code == "BANK")).Balance.Should().Be(1000);
        (await context.PaymentTransactions.CountAsync()).Should().Be(0); (await context.JournalEntries.CountAsync()).Should().Be(4);
        var bankItems = check.ServiceProvider.GetRequiredService<OpeningBankItemService>();
        var deposits = await context.OpeningBankItems.Where(x => x.Kind == "ReceivedCheque").ToListAsync(); deposits.Should().HaveCount(2); deposits.Select(x => x.ReceivedChequeId).Distinct().Should().HaveCount(2);
        foreach (var item in deposits) await bankItems.ResolveAsync(item.Id, new(Opening, false, "Cleared on statement"), default);
        (await context.JournalEntries.CountAsync()).Should().Be(4); (await context.ChequeRegisters.CountAsync(x => x.Status == ChequeStatus.Cleared)).Should().Be(2);
        var issued = await context.OpeningBankItems.SingleAsync(x => x.Kind == "IssuedCheque");
        await bankItems.ResolveAsync(issued.Id, new(Opening.AddDays(1), true, "Legacy cheque cancelled"), default);
        (await context.Accounts.SingleAsync(x => x.Code == "BANK")).Balance.Should().Be(1100);
        (await check.ServiceProvider.GetRequiredService<SupplierBalanceService>().GetAsync(issued.SupplierId!.Value, Opening.AddDays(1), default)).NetBalance.Should().Be(600);
        (await context.JournalEntries.CountAsync()).Should().Be(5);
        (await check.ServiceProvider.GetRequiredService<MigrationCentreService>().ReconcileScopeAsync(id, "Bank", "BANK", default)).Should().OnlyContain(x => x.Difference == 0);
    }
    [CustomerMigrationSqlFact]
    public async Task SupplierDueDate_IsNeverInvented_AndManagedArCannotBeFilledByTrialBalance()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "SupplierOpenings", "TrialBalance");
        using (var scope = provider.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); (await db.Suppliers.SingleAsync()).LegacySupplierCode = "S001"; await db.SaveChangesAsync(default); }
        await Upload(provider, id, "SupplierOpenings", "Code,Reference,Kind,DocumentDate,Amount\nS001,B-OLD,Bill,2026-09-01,500\n");
        using (var scope = provider.CreateScope()) { var preview = await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ValidateAsync(id, new() { Modules = ["SupplierOpenings"] }, default); preview.Units.SelectMany(x => x.Errors).Should().Contain(x => x.Message.Contains("Supplier due date")); }
        await Upload(provider, id, "TrialBalance", "Code,Debit,Credit\nAR,100,0\nSAFE,0,0\nBANK,0,0\nOBE,0,100\nREV,0,0\nAP,0,0\n");
        using var check = provider.CreateScope(); var tb = await check.ServiceProvider.GetRequiredService<MigrationCentreService>().ValidateAsync(id, new() { Modules = ["TrialBalance"] }, default);
        tb.Units.SelectMany(x => x.Errors).Should().Contain(x => x.Message.Contains("control") || x.Message.Contains("Control"));
        (await check.ServiceProvider.GetRequiredService<IErpDbContext>().JournalEntries.CountAsync()).Should().Be(0);
    }
    [CustomerMigrationSqlFact]
    public async Task UnusedSupplierOpening_ReversesWithHistory_ThenCorrectedIdentityCanBeImported()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "SupplierOpenings");
        using (var scope = provider.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); (await db.Suppliers.SingleAsync()).LegacySupplierCode = "S001"; await db.SaveChangesAsync(default); }
        await Upload(provider, id, "SupplierOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,DueDate,Amount\nOLD,S001,B-OLD,Bill,2026-09-01,2026-10-01,500\n"); await Post(provider, id, new() { Modules = ["SupplierOpenings"] });
        using (var scope = provider.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>(); var unit = await db.MigrationUnits.SingleAsync(); await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ReverseUnitAsync(id, unit.Id, new(Opening.AddDays(1), "Correct unused source"), default); var balance = scope.ServiceProvider.GetRequiredService<SupplierBalanceService>(); (await balance.GetAsync((await db.Suppliers.SingleAsync()).Id, Opening, default)).NetBalance.Should().Be(500); var after = await balance.GetAsync((await db.Suppliers.SingleAsync()).Id, Opening.AddDays(1), default); after.NetBalance.Should().Be(0); after.Events.Should().HaveCount(2); }
        await Upload(provider, id, "SupplierOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,DueDate,Amount\nOLD,S001,B-OLD,Bill,2026-09-01,2026-10-01,450\n"); await Post(provider, id, new() { Modules = ["SupplierOpenings"] });
        using var check = provider.CreateScope(); var context = check.ServiceProvider.GetRequiredService<IErpDbContext>(); (await context.MigrationOrigins.CountAsync()).Should().Be(2); (await context.MigrationOrigins.CountAsync(x => x.IsCurrent)).Should().Be(1); (await context.Accounts.SingleAsync(x => x.Code == "AP")).Balance.Should().Be(-450);
    }
    [CustomerMigrationSqlFact]
    public async Task BulkRelease_UsesIndependentSchedule_AndKeepsMismatchedPartyLocked()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); using var provider = Provider(database); var id = await Create(provider, "CustomerOpenings", "ControlTotals");
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nA,C001,A,Invoice,2026-09-01,100\nB,C002,B,Invoice,2026-09-01,200\n");
        await Upload(provider, id, "ControlTotals", "Kind,Code,Component,Amount,RowCount\nCustomer,C001,CustomerOpenings:Debit,100,1\nCustomer,C001,CustomerOpenings:Credit,0,1\nCustomer,C001,ReceivedCheques,0,0\nCustomer,C002,CustomerOpenings:Debit,199,1\nCustomer,C002,CustomerOpenings:Credit,0,1\nCustomer,C002,ReceivedCheques,0,0\n");
        await Post(provider, id, new() { Modules = ["CustomerOpenings", "ControlTotals"] });
        using var check = provider.CreateScope(); var result = await check.ServiceProvider.GetRequiredService<MigrationCentreService>().ReleaseSelectedAsync(id, new(new() { Modules = ["CustomerOpenings"] }, "Independent signed closing schedule"), default); result.Released.Should().Be(1, string.Join("; ", result.Pending)); result.Pending.Should().ContainSingle(); var db = check.ServiceProvider.GetRequiredService<IErpDbContext>(); (await db.MigrationScopes.SingleAsync(x => x.Code == "C001")).Status.Should().Be("Released"); (await db.MigrationScopes.SingleAsync(x => x.Code == "C002")).Status.Should().Be("Pending");
    }
}
