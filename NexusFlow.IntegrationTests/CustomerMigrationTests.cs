using System.Text;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NexusFlow.AppCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Features.Finance.CustomerMigration;
using NexusFlow.AppCore.Features.Finance.Banks.Commands;
using NexusFlow.AppCore.Features.Finance.Commands;
using NexusFlow.AppCore.Features.Reporting.Queries;
using NexusFlow.AppCore.Features.Sales.Queries;
using NexusFlow.AppCore.Features.Sales.Customers.Commands;
using NexusFlow.AppCore.Features.Treasury.Commands;
using NexusFlow.AppCore.Features.Treasury.Queries;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Services;
using NexusFlow.Domain.Enums;
using NexusFlow.Infrastructure.Services;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.IntegrationTests;

public sealed class CustomerMigrationSqlFactAttribute : FactAttribute
{
    public CustomerMigrationSqlFactAttribute() { if (Environment.GetEnvironmentVariable("NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS") != "1") Skip = "Opt-in disposable MSSQLLocalDB: NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS=1."; }
}
public sealed class CustomerMigrationSourceFactAttribute : FactAttribute
{
    public CustomerMigrationSourceFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXUSFLOW_CUSTOMER_SOURCE_FOLDER"))) Skip = "Optional private source report verification; no business data is committed."; }
}
public class CustomerMigrationTests
{
    [Fact]
    public void AppCore_ResolvesCustomerMigrationServices_FromApplicationRegistrations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppCore();
        services.AddSingleton(Mock.Of<IErpDbContext>());
        services.AddSingleton(Mock.Of<ICustomerMigrationFileReader>());
        services.AddSingleton(Mock.Of<IJournalService>());
        services.AddSingleton(Mock.Of<ICurrentUserService>());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<CustomerMasterImportService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<CustomerMigrationService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<CustomerBalanceService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<CustomerSettlementService>().Should().NotBeNull();
    }

    private static readonly DateTime Cutover = new(2026, 10, 1);
    private static ServiceProvider Provider(CustomerMigrationTestDatabase database, IJournalService? journals = null)
    {
        var services = new ServiceCollection(); database.Configure(services); if (journals != null) services.AddSingleton(journals); return services.BuildServiceProvider();
    }
    private static async Task<CustomerMigrationReview> Preview(ServiceProvider provider, string reportDate = "30/09/2026")
    {
        using var scope = provider.CreateScope(); var files = CustomerMigrationFixtures.Files(reportDate);
        return await scope.ServiceProvider.GetRequiredService<CustomerMigrationService>().CreateAsync(files.Outstanding, "cusout.xls", files.Cheques, "cheque.xlsx", Cutover, Cutover.AddDays(-1), default);
    }
    private static async Task<CustomerMigrationReview> Post(ServiceProvider provider, CustomerMigrationReview? preview = null)
    {
        preview ??= await Preview(provider); preview.Errors.Should().BeEmpty(); preview.CanPost.Should().BeTrue();
        using var scope = provider.CreateScope(); return await scope.ServiceProvider.GetRequiredService<CustomerMigrationService>().PostAsync(preview.BatchId, preview.ReviewHash, true, default);
    }
    [Fact]
    public void Reports_ReadConfirmedAmounts_Dates_Subtotals_AndTextChequeNumbers()
    {
        var files = CustomerMigrationFixtures.Files(); var source = new CustomerMigrationFileReader().Read(files.Outstanding, files.Cheques);
        source.Errors.Should().BeEmpty(string.Join("; ", source.Errors)); source.Outstanding.Sum(x => x.Amount).Should().Be(19051.50m);
        source.Outstanding[0].Amount.Should().Be(18751.50m); source.Outstanding[0].DebitAmount.Should().Be(128880m);
        source.Outstanding[2].Kind.Should().Be("Goods-return credit"); source.Cheques[0].ChequeNumber.Should().Be("001946A");
        source.Cheques[1].ChequeNumber.Should().Be("000012"); source.Cheques.Sum(x => x.Amount).Should().Be(750);
        source.Cheques[1].ChequeDate.Should().Be(new DateTime(2026, 10, 15)); source.Cheques[0].ReceivedDate.Should().Be(new DateTime(2026, 9, 20));
        var corrupt = CustomerMigrationFixtures.Files(corruptSubtotal: true); new CustomerMigrationFileReader().Read(corrupt.Outstanding, corrupt.Cheques).Errors.Should().Contain(x => x.Contains("subtotal"));
        var formula = CustomerMigrationFixtures.Files(formula: true); new CustomerMigrationFileReader().Read(formula.Outstanding, formula.Cheques).Errors.Should().NotBeEmpty();
    }
    [CustomerMigrationSourceFact]
    public void PrivateLegacyFiles_ReconcileOriginalAssessment()
    {
        var folder = Environment.GetEnvironmentVariable("NEXUSFLOW_CUSTOMER_SOURCE_FOLDER")!;
        var source = new CustomerMigrationFileReader().Read(File.ReadAllBytes(Path.Combine(folder, "cusout.xls")), File.ReadAllBytes(Path.Combine(folder, "cheque in hand.xlsx")));
        source.Errors.Should().BeEmpty(string.Join("; ", source.Errors)); source.Outstanding.Should().HaveCount(1150); source.Cheques.Should().HaveCount(49);
        source.Outstanding.Sum(x => x.Amount).Should().Be(162114985.14m); source.Cheques.Sum(x => x.Amount).Should().Be(5328628m);
        source.Outstanding.Where(x => x.Amount < 0).Should().HaveCount(20); source.Outstanding.Where(x => x.Amount < 0).Sum(x => x.Amount).Should().Be(-321597.10m);
        source.ChequeReportDate.Should().Be(new DateTime(2026, 9, 28));
    }
    [CustomerMigrationSqlFact]
    public async Task Posting_IsAtomicIdempotent_AndNeverCreatesSalesOrReceipts()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database);
        var preview = await Preview(provider); var repeated = await Preview(provider); repeated.BatchId.Should().Be(preview.BatchId);
        await Post(provider, preview); await Post(provider, preview);
        await using var db = database.Open(); (await db.CustomerOpeningItems.CountAsync()).Should().Be(4); (await db.ChequeRegisters.CountAsync()).Should().Be(2);
        (await db.PaymentTransactions.CountAsync()).Should().Be(0); (await db.SalesInvoices.CountAsync()).Should().Be(0); (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.Accounts.SingleAsync(x => x.Code == "AR")).Balance.Should().Be(19051.50m); (await db.Accounts.SingleAsync(x => x.Code == "SAFE")).Balance.Should().Be(750);
        (await db.Accounts.SingleAsync(x => x.Code == "OBE")).Balance.Should().Be(-19801.50m);
        (await db.CustomerOpeningItems.SingleAsync(x => x.ReferenceNo == "GT00011762")).DueDate.Should().Be(Cutover);
        (await db.ChequeRegisters.ToListAsync()).Should().OnlyContain(x => x.OriginalReceiptId == null);
        using var scope = provider.CreateScope(); var reconciliation = await scope.ServiceProvider.GetRequiredService<CustomerMigrationService>().ReconcileAsync(preview.BatchId, default);
        System.Text.Json.JsonSerializer.SerializeToElement(reconciliation).GetProperty("Matches").GetBoolean().Should().BeTrue();
    }
    [CustomerMigrationSqlFact]
    public async Task FailedJournal_RollsBackItemsChequesAndGl()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize();
        var journal = new Mock<IJournalService>(); journal.Setup(x => x.PostJournalAsync(It.IsAny<JournalEntryRequest>())).ReturnsAsync(Result<int>.Failure("Injected failure"));
        await using var provider = Provider(database, journal.Object); var preview = await Preview(provider);
        Func<Task> act = async () => await Post(provider, preview); await act.Should().ThrowAsync<InvalidOperationException>();
        await using var db = database.Open(); (await db.CustomerOpeningItems.CountAsync()).Should().Be(0); (await db.ChequeRegisters.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0); (await db.CustomerMigrationBatches.SingleAsync()).Status.Should().Be("Draft");
        (await db.Accounts.ToListAsync()).Should().OnlyContain(x => x.Balance == 0);
    }
    [CustomerMigrationSqlFact]
    public async Task Preflight_BlocksOldSnapshots_DisabledPosting_MissingMapping_AndStaleReview()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database);
        (await Preview(provider, "28/09/2026")).Errors.Should().Contain(x => x.Contains("report date"));
        var preview = await Preview(provider);
        await using (var db = database.Open()) { var c = await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C001"); c.Name = "Renamed customer"; await db.SaveChangesAsync(); }
        using (var scope = provider.CreateScope()) { Func<Task> act = async () => await scope.ServiceProvider.GetRequiredService<CustomerMigrationService>().PostAsync(preview.BatchId, preview.ReviewHash, true, default); await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*changed*"); }
        await using (var db = database.Open()) { (await db.SystemConfigs.SingleAsync(x => x.Key == CustomerMigrationService.EnableKey)).Value = "false"; (await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C002")).LegacyCustomerCode = null; await db.SaveChangesAsync(); }
        using (var scope = provider.CreateScope()) { var review = await scope.ServiceProvider.GetRequiredService<CustomerMigrationService>().ReviewAsync(preview.BatchId, null, default); review.CanPost.Should().BeFalse(); review.Errors.Should().Contain(x => x.Contains("C002")); }
    }
    [CustomerMigrationSqlFact]
    public async Task CreditsAndReceipts_SettleOpeningDebt_WithDatedReversalAndStatement()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database); await Post(provider);
        using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusFlow.Infrastructure.ErpDbContext>(); var settlement = scope.ServiceProvider.GetRequiredService<CustomerSettlementService>();
        var customer = await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C001"); var credit = await db.CustomerOpeningItems.SingleAsync(x => x.Amount < 0); var debt = await db.CustomerOpeningItems.SingleAsync(x => x.ReferenceNo == "GT00011762");
        await settlement.ApplyCreditAsync(customer.Id, credit.Id, Cutover, [new() { OpeningItemId = debt.Id, Amount = 300 }], default);
        var balances = scope.ServiceProvider.GetRequiredService<CustomerBalanceService>(); (await balances.GetAsync(customer.Id, Cutover)).NetBalance.Should().Be(18851.50m);
        (await balances.GetAsync(customer.Id, Cutover)).Debits.Single(x => x.Id == debt.Id && x.TargetType == "OpeningItem").Balance.Should().Be(18451.50m);
        Func<Task> excess = async () => await settlement.ApplyCreditAsync(customer.Id, credit.Id, Cutover, [new() { OpeningItemId = debt.Id, Amount = 1 }], default); await excess.Should().ThrowAsync<InvalidOperationException>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>(); var cash = await db.Accounts.SingleAsync(x => x.Code == "BANK");
        var receipt = await mediator.Send(new RecordPaymentCommand { CustomerId = customer.Id, Date = Cutover, Type = PaymentType.CustomerReceipt, Method = PaymentMethod.Cash, AccountId = cash.Id, ReceiptAmount = 1000, Allocations = [new() { OpeningItemId = debt.Id, Amount = 1000 }] }); receipt.Succeeded.Should().BeTrue(receipt.Message);
        var detail = await mediator.Send(new GetReceiptByIdQuery { ReceiptId = receipt.Data }); detail.Data.Allocations.Single().InvoiceNumber.Should().Be(debt.ReferenceNo);
        var voided = await mediator.Send(new VoidReceiptCommand(receipt.Data, Cutover.AddDays(3))); voided.Succeeded.Should().BeTrue(voided.Message);
        (await balances.GetAsync(customer.Id, Cutover)).NetBalance.Should().Be(17851.50m); (await balances.GetAsync(customer.Id, Cutover.AddDays(3))).NetBalance.Should().Be(18851.50m);
        var application = await db.CustomerCreditApplications.SingleAsync(); await settlement.ReverseCreditAsync(application.Id, Cutover.AddDays(4), "Correction", default);
        (await balances.GetAsync(customer.Id, Cutover)).OpeningCredits.Single().Available.Should().Be(0); (await balances.GetAsync(customer.Id, Cutover.AddDays(4))).OpeningCredits.Single().Available.Should().Be(300);
        var statement = await mediator.Send(new GetCustomerStatementQuery { CustomerId = customer.Id, StartDate = Cutover, EndDate = Cutover.AddDays(4) }); statement.Data.Last().Balance.Should().Be(18851.50m);
        var ageing = await mediator.Send(new GetArAgingQuery { CustomerId = customer.Id, AsOfDate = Cutover }); ageing.Data.Single().TotalOutstanding.Should().Be(17851.50m);
    }
    [CustomerMigrationSqlFact]
    public async Task OpeningCheque_BounceCreatesCollectibleMemo_WithoutReopeningHistoricalInvoices()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database); await Post(provider);
        using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusFlow.Infrastructure.ErpDbContext>(); var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var cheque = await db.ChequeRegisters.FirstAsync(); var bounce = await mediator.Send(new BounceChequeCommand { ChequeId = cheque.Id, BounceDate = Cutover.AddDays(2), Reason = "Insufficient funds" }); bounce.Succeeded.Should().BeTrue(bounce.Message);
        var repeated = await mediator.Send(new BounceChequeCommand { ChequeId = cheque.Id, BounceDate = Cutover.AddDays(2), Reason = "Repeat" }); repeated.Succeeded.Should().BeFalse();
        var memo = await db.CustomerDebitMemos.SingleAsync(); memo.Amount.Should().Be(cheque.Amount); memo.DueDate.Should().Be(Cutover.AddDays(2));
        var unpaid = await mediator.Send(new GetUnpaidInvoicesQuery { CustomerId = cheque.CustomerId }); unpaid.Data.Invoices.Should().Contain(x => x.TargetType == "DebitMemo" && x.Id == memo.Id);
        var bank = await db.Accounts.SingleAsync(x => x.Code == "BANK"); var paid = await mediator.Send(new RecordPaymentCommand { CustomerId = cheque.CustomerId, Date = Cutover.AddDays(3), Type = PaymentType.CustomerReceipt, Method = PaymentMethod.Cash, AccountId = bank.Id, ReceiptAmount = cheque.Amount, Allocations = [new() { DebitMemoId = memo.Id, Amount = cheque.Amount }] }); paid.Succeeded.Should().BeTrue(paid.Message);
        memo.AmountPaid.Should().Be(cheque.Amount); memo.PaymentStatus.Should().Be(InvoicePaymentStatus.Paid); (await db.SalesInvoices.CountAsync()).Should().Be(0);
    }
    [CustomerMigrationSqlFact]
    public async Task OpeningCheque_DepositReconcilesUsingExactJournal_AndEndorsementBouncesWithoutReceipt()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database); await Post(provider);
        using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusFlow.Infrastructure.ErpDbContext>(); var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var cheques = await db.ChequeRegisters.OrderBy(x => x.Id).ToListAsync(); var bank = await db.Accounts.SingleAsync(x => x.Code == "BANK");
        (await mediator.Send(new DepositChequeCommand(cheques[0].Id, bank.Id, Cutover.AddDays(15)))).Succeeded.Should().BeTrue();
        var line = await db.JournalLines.SingleAsync(x => x.JournalEntryId == cheques[0].DepositJournalEntryId && x.AccountId == bank.Id);
        var cleared = await mediator.Send(new FinalizeReconciliationCommand { BankAccountId = bank.Id, StatementDate = Cutover.AddDays(16), ClearedJournalLineIds = [line.Id] }); cleared.Succeeded.Should().BeTrue(cleared.Message); cheques[0].Status.Should().Be(ChequeStatus.Cleared);
        var supplier = await db.Suppliers.SingleAsync(); (await mediator.Send(new EndorseChequeCommand { ChequeId = cheques[1].Id, SupplierId = supplier.Id, EndorsementDate = Cutover.AddDays(15) })).Succeeded.Should().BeTrue();
        var bounce = await mediator.Send(new BounceChequeCommand { ChequeId = cheques[1].Id, BounceDate = Cutover.AddDays(17), Reason = "Endorsed cheque returned" }); bounce.Succeeded.Should().BeTrue(bounce.Message);
        (await db.CustomerDebitMemos.SingleAsync()).Amount.Should().Be(cheques[1].Amount); (await db.PaymentTransactions.SingleAsync()).IsVoided.Should().BeTrue();
    }
    [CustomerMigrationSqlFact]
    public async Task UnusedBatchReversal_RetainsHistory_AndSignedOffBatchLocksPosting()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database); var batch = await Post(provider);
        using (var scope = provider.CreateScope()) { await scope.ServiceProvider.GetRequiredService<CustomerMigrationService>().ReverseAsync(batch.BatchId, Cutover, "Wrong source", default); }
        await using (var db = database.Open()) { (await db.Accounts.ToListAsync()).Should().OnlyContain(x => x.Balance == 0); var customer = await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C001"); var balances = new CustomerBalanceService(db); (await balances.GetAsync(customer.Id, Cutover)).NetBalance.Should().Be(0); (await balances.GetAsync(customer.Id, Cutover.AddDays(2))).NetBalance.Should().Be(0); }
        var replacement = await Post(provider);
        using (var scope = provider.CreateScope()) { var migration = scope.ServiceProvider.GetRequiredService<CustomerMigrationService>(); await migration.SignOffAsync(replacement.BatchId, default); Func<Task> reverse = () => migration.ReverseAsync(replacement.BatchId, Cutover.AddDays(3), "Later", default); await reverse.Should().ThrowAsync<InvalidOperationException>(); }
        (await Preview(provider, "29/09/2026")).Errors.Should().Contain(x => x.Contains("signed off"));
    }
    [CustomerMigrationSqlFact]
    public async Task MasterCsv_ImportsLegacyCodes_AndRequiresExplicitUpdateId()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database); using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusFlow.Infrastructure.ErpDbContext>(); var customer = await db.Customers.FirstAsync();
        var row = new SaveCustomerCommand { Name = "New customer", LegacyCustomerCode = "old001", TaxRegNo = "N/A", CustomerGroupId = customer.CustomerGroupId, PriceLevelId = customer.PriceLevelId,
            ContactPerson = "Contact", Email = "new@example.com", Phone = "0771234567", AddressLine1 = "Road", Province = "Western", District = "Colombo", City = "Colombo", DefaultReceivableAccountId = customer.DefaultReceivableAccountId, DefaultRevenueAccountId = customer.DefaultRevenueAccountId, PaymentTermId = customer.PaymentTermId };
        static byte[] Csv(SaveCustomerCommand row) { var p = typeof(SaveCustomerCommand); var values = CustomerMasterImportService.Headers.Split(',').Select(name => Convert.ToString(p.GetProperty(name)!.GetValue(row), System.Globalization.CultureInfo.InvariantCulture) ?? ""); return Encoding.UTF8.GetBytes(CustomerMasterImportService.Headers + "\n" + string.Join(',', values) + "\n"); }
        var import = scope.ServiceProvider.GetRequiredService<CustomerMasterImportService>(); var bytes = Csv(row); var preview = await import.PreviewAsync(bytes, default); preview.Errors.Should().BeEmpty(); await import.ExecuteAsync(bytes, preview.PreviewHash, default);
        (await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "OLD001")).Name.Should().Be("New customer"); (await import.PreviewAsync(bytes, default)).Errors.Should().Contain(x => x.Contains("explicit Id"));
        row.Id = (await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "OLD001")).Id; row.Name = "Updated customer"; bytes = Csv(row); preview = await import.PreviewAsync(bytes, default); preview.Errors.Should().BeEmpty(); await import.ExecuteAsync(bytes, preview.PreviewHash, default);
    }
    [CustomerMigrationSqlFact]
    public async Task ConcurrentPosting_WritesExactlyOneOpeningJournal()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database); var preview = await Preview(provider);
        async Task<bool> TryPost() { try { using var scope = provider.CreateScope(); await scope.ServiceProvider.GetRequiredService<CustomerMigrationService>().PostAsync(preview.BatchId, preview.ReviewHash, true, default); return true; } catch (Exception) { return false; } }
        (await Task.WhenAll(TryPost(), TryPost())).Should().Contain(true);
        await using var db = database.Open(); (await db.JournalEntries.CountAsync()).Should().Be(1); (await db.CustomerOpeningItems.CountAsync()).Should().Be(4); (await db.ChequeRegisters.CountAsync()).Should().Be(2);
    }
    [CustomerMigrationSqlFact]
    public async Task DatedBatchReversal_PreservesPriorBalance_AndBlocksBackdatedReplacement()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database); var batch = await Post(provider);
        using var scope = provider.CreateScope(); var migration = scope.ServiceProvider.GetRequiredService<CustomerMigrationService>();
        await migration.ReverseAsync(batch.BatchId, Cutover.AddDays(2), "Later reversal", default);
        var db = scope.ServiceProvider.GetRequiredService<NexusFlow.Infrastructure.ErpDbContext>(); var customer = await db.Customers.FirstAsync();
        var balances = scope.ServiceProvider.GetRequiredService<CustomerBalanceService>();
        (await balances.GetAsync(customer.Id, Cutover)).NetBalance.Should().Be(18851.50m);
        (await balances.GetAsync(customer.Id, Cutover.AddDays(2))).NetBalance.Should().Be(0);
        (await Preview(provider)).Errors.Should().Contain(x => x.Contains("backdated replacement"));
    }
    [CustomerMigrationSqlFact]
    public async Task DatedCreditReversal_CannotBeReusedBeforeItsReversalDate()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database); await Post(provider);
        using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusFlow.Infrastructure.ErpDbContext>();
        var credit = await db.CustomerOpeningItems.SingleAsync(x => x.Amount < 0); var debt = await db.CustomerOpeningItems.SingleAsync(x => x.ReferenceNo == "GT00011762");
        var service = scope.ServiceProvider.GetRequiredService<CustomerSettlementService>();
        await service.ApplyCreditAsync(credit.CustomerId, credit.Id, Cutover, [new() { OpeningItemId = debt.Id, Amount = 300 }], default);
        await service.ReverseCreditAsync((await db.CustomerCreditApplications.SingleAsync()).Id, Cutover.AddDays(5), "Correction", default);
        Func<Task> act = async () => await service.ApplyCreditAsync(credit.CustomerId, credit.Id, Cutover.AddDays(3), [new() { OpeningItemId = debt.Id, Amount = 300 }], default);
        await act.Should().ThrowAsync<InvalidOperationException>();
        await service.ApplyCreditAsync(credit.CustomerId, credit.Id, Cutover.AddDays(5), [new() { OpeningItemId = debt.Id, Amount = 300 }], default);
    }
    [CustomerMigrationSqlFact]
    public async Task InvalidAllocationsAndDuplicateControlImport_AreRejected()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database); await Post(provider);
        using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusFlow.Infrastructure.ErpDbContext>(); var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var first = await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C001"); var otherDebt = await db.CustomerOpeningItems.SingleAsync(x => x.LegacyCustomerCode == "C002");
        var ar = await db.Accounts.SingleAsync(x => x.Code == "AR"); var safe = await db.Accounts.SingleAsync(x => x.Code == "SAFE");
        var wrong = await mediator.Send(new RecordPaymentCommand { CustomerId = first.Id, Date = Cutover, ReceiptAmount = 100, Type = PaymentType.CustomerReceipt, Method = PaymentMethod.Cash, AccountId = ar.Id, Allocations = [new() { OpeningItemId = otherDebt.Id, Amount = 100 }] }); wrong.Succeeded.Should().BeFalse();
        var sameAccount = await mediator.Send(new RecordPaymentCommand { CustomerId = first.Id, Date = Cutover, ReceiptAmount = 100, Type = PaymentType.CustomerReceipt, Method = PaymentMethod.Cash, AccountId = ar.Id }); sameAccount.Succeeded.Should().BeFalse();
        var tb = await mediator.Send(new ImportTrialBalanceCommand { CutoverDate = Cutover, Lines = [new() { AccountCode = "SAFE", Debit = 750 }, new() { AccountCode = "OBE", Credit = 750 }] }); tb.Succeeded.Should().BeFalse();
        (await db.PaymentTransactions.CountAsync()).Should().Be(0);
    }
    [CustomerMigrationSqlFact]
    public async Task NewChequeReceipt_BounceVoidsReceipt_AndRestoresAllocatedOpeningDebt()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var provider = Provider(database); await Post(provider);
        using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusFlow.Infrastructure.ErpDbContext>(); var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var customer = await db.Customers.SingleAsync(x => x.LegacyCustomerCode == "C001"); var debt = await db.CustomerOpeningItems.SingleAsync(x => x.ReferenceNo == "DE1");
        var bank = await db.Accounts.SingleAsync(x => x.Code == "BANK"); var branch = await db.BankBranches.SingleAsync();
        var receipt = await mediator.Send(new RecordPaymentCommand { CustomerId = customer.Id, Date = Cutover, Type = PaymentType.CustomerReceipt, Method = PaymentMethod.Cheque,
            ReceiptAmount = 250, ChequeNumber = "000099", ChequeDate = Cutover.AddDays(2), BankBranchId = branch.Id, Allocations = [new() { OpeningItemId = debt.Id, Amount = 250 }] });
        receipt.Succeeded.Should().BeTrue(receipt.Message);
        var cheque = await db.ChequeRegisters.SingleAsync(x => x.OriginalReceiptId == receipt.Data); cheque.MigrationBatchId.Should().BeNull();
        (await mediator.Send(new DepositChequeCommand(cheque.Id, bank.Id, Cutover.AddDays(2)))).Succeeded.Should().BeTrue();
        var result = await mediator.Send(new BounceChequeCommand { ChequeId = cheque.Id, BounceDate = Cutover.AddDays(3), Reason = "New receipt cheque returned" }); result.Succeeded.Should().BeTrue(result.Message);
        (await db.PaymentTransactions.SingleAsync()).VoidDate.Should().Be(Cutover.AddDays(3)); debt.AmountSettled.Should().Be(0); (await db.CustomerDebitMemos.CountAsync()).Should().Be(0);
        var balances = scope.ServiceProvider.GetRequiredService<CustomerBalanceService>();
        (await balances.GetAsync(customer.Id, Cutover)).NetBalance.Should().Be(18601.50m); (await balances.GetAsync(customer.Id, Cutover.AddDays(3))).NetBalance.Should().Be(18851.50m);
    }
    [CustomerMigrationSqlFact]
    public async Task SchemaScripts_UpgradeExistingTemporalTables_AndRestoreTheNewModel()
    {
        await using var database = new CustomerMigrationTestDatabase(); await database.Initialize(); await using var db = database.Open();
        var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
        var ids = db.Database.GetMigrations().ToArray(); var baseline = ids[^3]; var latest = ids[^1];
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE [__EFMigrationsHistory] ([MigrationId] nvarchar(150) NOT NULL PRIMARY KEY, [ProductVersion] nvarchar(32) NOT NULL)");
        foreach (var id in ids) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [__EFMigrationsHistory] VALUES ({id}, {"10.0.5"})");
        async Task Run(string script)
        {
            await db.Database.OpenConnectionAsync();
            foreach (var batch in System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
            await db.Database.CloseConnectionAsync();
        }
        // Recreate the empty prior schema one migration at a time, then rehearse the combined forward upgrade.
        await Run(migrator.GenerateScript(latest, ids[^2]));
        await Run(migrator.GenerateScript(ids[^2], baseline));
        await Run(migrator.GenerateScript(baseline, latest));
        (await db.CustomerOpeningItems.CountAsync()).Should().Be(0); (await db.CustomerCreditApplications.CountAsync()).Should().Be(0);
        (await db.ChequeRegisters.CountAsync()).Should().Be(0); (await db.Customers.AsNoTracking().ToListAsync()).Should().HaveCount(2);
    }
}
