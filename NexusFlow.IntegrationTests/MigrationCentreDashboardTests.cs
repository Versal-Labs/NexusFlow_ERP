using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Dashboard.Queries;
using NexusFlow.AppCore.Features.Migration;
using NexusFlow.AppCore.Features.Reporting.Queries;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;

namespace NexusFlow.IntegrationTests;

public sealed partial class MigrationCentreTests
{
    private static async Task<DashboardMetricsDto> Dashboard(IServiceProvider services)
    {
        var result = await new GetDashboardDataHandler(services.GetRequiredService<IErpDbContext>(), services.GetRequiredService<IFinancialPeriodService>())
            .Handle(new(), default);
        result.Succeeded.Should().BeTrue(result.Message);
        return result.Data;
    }

    private static async Task ConfigureDashboardCashGroup(IErpDbContext db)
    {
        var group = new Account { Code = "CASH-GROUP", Name = "Liquid funds", Type = AccountType.Asset };
        group.UpdateBalance(10000m); // A folder balance must never be counted alongside its children.
        db.Accounts.Add(group);
        await db.SaveChangesAsync(default);
        var nested = new Account { Code = "NESTED", Name = "Cash locations", Type = AccountType.Asset, ParentAccountId = group.Id };
        db.Accounts.Add(nested);
        await db.SaveChangesAsync(default);
        var petty = new Account { Code = "TILL-A", Name = "Till", Type = AccountType.Asset, IsTransactionAccount = true, ParentAccountId = nested.Id };
        petty.UpdateBalance(30m);
        var inventory = new Account { Code = "OTHER-ASSET", Name = "Inventory", Type = AccountType.Asset, IsTransactionAccount = true };
        inventory.UpdateBalance(500m);
        db.Accounts.AddRange(petty, inventory);
        (await db.Accounts.SingleAsync(x => x.Code == "BANK")).ParentAccountId = group.Id;
        (await db.Accounts.SingleAsync(x => x.Code == "SAFE")).ParentAccountId = group.Id;
        db.SystemConfigs.Add(new SystemConfig { Key = ConfigurationKeys.DashboardCashBankAccountRoot, Value = group.Code });
        await db.SaveChangesAsync(default);
    }

    [CustomerMigrationSqlFact]
    public async Task Dashboard_IncludesMigratedNetBalances_AndKeepsSafeChequesOutOfCashAndAr()
    {
        await using var database = new CustomerMigrationTestDatabase();
        await database.Initialize();
        using var provider = Provider(database);
        var id = await Create(provider, "CustomerOpenings", "SupplierOpenings", "ReceivedCheques", "BankOpening");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
            (await db.Suppliers.SingleAsync()).LegacySupplierCode = "S001";
            await ConfigureDashboardCashGroup(db);
            var settings = MigrationJson.Read<MigrationSettings>((await db.MigrationProjects.SingleAsync()).SettingsJson);
            settings.Mappings["BankBranch"] = new() { ["7010-001"] = await db.BankBranches.Select(x => x.Id).SingleAsync() };
            await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().SaveSettingsAsync(id, settings, default);
        }
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nI,C001,INV,Invoice,2026-09-01,1000\nC,C001,RET,ReturnCredit,2026-09-01,-100\n");
        await Upload(provider, id, "SupplierOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,DueDate,Amount\nB,S001,BILL,Bill,2026-09-01,2026-10-01,500\nSC,S001,RETURN,Credit,2026-09-01,,-50\n");
        await Upload(provider, id, "ReceivedCheques", "LegacyRecordId,Code,Reference,ChequeNumber,ChequeDate,ReceivedDate,BankBranchCode,Amount,Status,PartyReflected,BankReflected\nQ,C001,REC,000001,2026-10-20,2026-09-25,7010-001,200,InSafe,true,false\n");
        await Upload(provider, id, "BankOpening", "Code,Amount,StatementBalance\nBANK,1000,1000\n");
        await Post(provider, id, new() { Modules = ["CustomerOpenings", "SupplierOpenings", "ReceivedCheques", "BankOpening"] });

        using var check = provider.CreateScope();
        var metrics = await Dashboard(check.ServiceProvider);
        metrics.AccountsReceivable.Should().Be(900m);
        metrics.AccountsPayable.Should().Be(450m);
        metrics.CashOnHand.Should().Be(1030m);
        metrics.CashAndBankWarning.Should().BeNull();
        var dbCheck = check.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await dbCheck.ChequeRegisters.SingleAsync()).Amount.Should().Be(200m);
        var ar = await new GetArAgingHandler(dbCheck).Handle(new(), default);
        var ap = await new GetApAgingHandler(dbCheck).Handle(new(), default);
        metrics.AccountsReceivable.Should().Be(ar.Data.Sum(x => x.TotalOutstanding));
        metrics.AccountsPayable.Should().Be(ap.Data.Sum(x => x.TotalOutstanding));
        metrics.OverdueReceivables.Should().Be(ar.Data.Sum(x => x.Days1To30 + x.Days31To60 + x.Days61To90 + x.Over90Days));
    }

    [CustomerMigrationSqlFact]
    public async Task Dashboard_UsesDatedSettlementsAndCredits_AndTreatsDueTodayAsCurrent()
    {
        await using var database = new CustomerMigrationTestDatabase();
        await database.Initialize();
        using var provider = Provider(database);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        var today = DateTime.UtcNow.Date;
        var customer = await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C001");
        var supplier = await db.Suppliers.SingleAsync();
        var oldInvoice = new SalesInvoice { CustomerId = customer.Id, InvoiceNumber = "OLD", InvoiceDate = today.AddDays(-10), DueDate = today.AddDays(-1), GrandTotal = 1000m, IsPosted = true, PaymentStatus = InvoicePaymentStatus.Paid, AmountPaid = 1000m };
        db.SalesInvoices.AddRange(oldInvoice,
            new() { CustomerId = customer.Id, InvoiceNumber = "DUE-TODAY", InvoiceDate = today.AddDays(-1), DueDate = today, GrandTotal = 200m, IsPosted = true },
            new() { CustomerId = customer.Id, InvoiceNumber = "FUTURE", InvoiceDate = today.AddDays(1), DueDate = today.AddDays(2), GrandTotal = 900m, IsPosted = true });
        var bill = new SupplierBill { SupplierId = supplier.Id, BillNumber = "OLD-BILL", BillDate = today.AddDays(-10), DueDate = today.AddDays(-1), GrandTotal = 500m, IsPosted = true, PaymentStatus = InvoicePaymentStatus.Paid, AmountPaid = 500m };
        db.SupplierBills.AddRange(bill, new() { SupplierId = supplier.Id, BillNumber = "FUTURE-BILL", BillDate = today.AddDays(1), DueDate = today.AddDays(2), GrandTotal = 800m, IsPosted = true });
        var receipt = new PaymentTransaction { CustomerId = customer.Id, Type = PaymentType.CustomerReceipt, ReferenceNo = "REC", Date = today.AddDays(-2), Amount = 100m };
        var payment = new PaymentTransaction { SupplierId = supplier.Id, Type = PaymentType.SupplierPayment, ReferenceNo = "PAY", Date = today.AddDays(-2), Amount = 75m };
        db.PaymentTransactions.AddRange(receipt, payment,
            new() { CustomerId = customer.Id, Type = PaymentType.CustomerReceipt, ReferenceNo = "UNAPPLIED", Date = today, Amount = 50m },
            new() { CustomerId = customer.Id, Type = PaymentType.CustomerReceipt, ReferenceNo = "REC-VOID", Date = today.AddDays(-4), Amount = 40m, IsVoided = true, VoidDate = today.AddDays(-1) },
            new() { SupplierId = supplier.Id, Type = PaymentType.SupplierPayment, ReferenceNo = "PAY-VOID", Date = today.AddDays(-4), Amount = 25m, IsVoided = true, VoidDate = today.AddDays(-1) },
            new() { CustomerId = customer.Id, Type = PaymentType.CustomerReceipt, ReferenceNo = "REC-FUTURE", Date = today.AddDays(1), Amount = 500m },
            new() { SupplierId = supplier.Id, Type = PaymentType.SupplierPayment, ReferenceNo = "PAY-FUTURE", Date = today.AddDays(1), Amount = 300m });
        db.PaymentAllocations.AddRange(new PaymentAllocation { PaymentTransaction = receipt, SalesInvoice = oldInvoice, AmountAllocated = 100m },
            new PaymentAllocation { PaymentTransaction = payment, SupplierBill = bill, AmountAllocated = 75m });
        db.CustomerDebitMemos.Add(new() { CustomerId = customer.Id, DebitMemoNumber = "MEMO", Date = today.AddDays(-3), DueDate = today.AddDays(-1), Amount = 30m, ReceivableAccountId = customer.DefaultReceivableAccountId });
        var warehouse = new Warehouse { Code = "RET", Name = "Returns" };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(default);
        db.CreditNotes.Add(new() { CustomerId = customer.Id, SalesInvoiceId = oldInvoice.Id, CreditNoteNumber = "RETURN", Date = today.AddDays(-1), GrandTotal = 20m, IsPosted = true, ReturnToWarehouseId = warehouse.Id });
        await db.SaveChangesAsync(default);

        var metrics = await Dashboard(scope.ServiceProvider);
        metrics.AccountsReceivable.Should().Be(1060m);
        metrics.AccountsPayable.Should().Be(425m);
        metrics.OverdueReceivables.Should().Be(930m);
        var ar = await new GetArAgingHandler(db).Handle(new(), default);
        var ap = await new GetApAgingHandler(db).Handle(new(), default);
        metrics.AccountsReceivable.Should().Be(ar.Data.Sum(x => x.TotalOutstanding));
        metrics.AccountsPayable.Should().Be(ap.Data.Sum(x => x.TotalOutstanding));
    }

    [CustomerMigrationSqlFact]
    public async Task Dashboard_ExcludesReversedOpenings_WithoutDeletingOriginalDocuments()
    {
        await using var database = new CustomerMigrationTestDatabase();
        await database.Initialize();
        using var provider = Provider(database);
        var id = await Create(provider, "CustomerOpenings", "SupplierOpenings");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
            (await db.Suppliers.SingleAsync()).LegacySupplierCode = "S001";
            await db.SaveChangesAsync(default);
        }
        await Upload(provider, id, "CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nI,C001,I,Invoice,2026-09-01,100\n");
        await Upload(provider, id, "SupplierOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,DueDate,Amount\nB,S001,B,Bill,2026-09-01,2026-10-01,50\n");
        await Post(provider, id, new() { Modules = ["CustomerOpenings", "SupplierOpenings"] });
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
            var ids = await db.MigrationUnits.Select(x => x.Id).ToListAsync();
            foreach (var unit in ids) await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ReverseUnitAsync(id, unit, new(Opening.AddDays(1), "Unused fixture reversed"), default);
        }
        using var check = provider.CreateScope();
        var metrics = await Dashboard(check.ServiceProvider);
        metrics.AccountsReceivable.Should().Be(0m);
        metrics.AccountsPayable.Should().Be(0m);
        metrics.OverdueReceivables.Should().Be(0m);
        var context = check.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await context.CustomerOpeningItems.CountAsync()).Should().Be(1);
        (await context.SupplierOpeningItems.CountAsync()).Should().Be(1);
    }

    [CustomerMigrationSqlFact]
    public async Task Dashboard_DoesNotPresentAllAssetsAsCash_WhenCashGroupIsMissing()
    {
        await using var database = new CustomerMigrationTestDatabase();
        await database.Initialize();
        using var provider = Provider(database);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        (await db.Accounts.SingleAsync(x => x.Code == "AR")).UpdateBalance(10000m);
        await db.SaveChangesAsync(default);
        var metrics = await Dashboard(scope.ServiceProvider);
        metrics.CashOnHand.Should().Be(0m);
        metrics.CashAndBankWarning.Should().NotBeNullOrWhiteSpace();

        await ConfigureDashboardCashGroup(db);
        (await db.SystemConfigs.SingleAsync(x => x.Key == AccountMappingKeys.UndepositedFunds)).Value = "MISSING-SAFE";
        await db.SaveChangesAsync(default);
        var invalidMapping = await Dashboard(scope.ServiceProvider);
        invalidMapping.CashAndBankWarning.Should().NotBeNullOrWhiteSpace();
        invalidMapping.CashOnHand.Should().Be(0m);
    }
}