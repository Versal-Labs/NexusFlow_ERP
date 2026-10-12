using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Features.Finance.CustomerMigration;

public sealed class CustomerMigrationService(IErpDbContext db, ICustomerMigrationFileReader reader,
    IJournalService journals, ICurrentUserService currentUser)
{
    public const string EnableKey = "Migration.Customer.Enabled";
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public async Task<CustomerMigrationReview> CreateAsync(byte[] outstanding, string outstandingName,
        byte[] cheques, string chequeName, DateTime cutover, DateTime asOf, CancellationToken ct)
    {
        if (db.MigrationProjects != null && await db.MigrationProjects.AnyAsync(ct)) throw new InvalidOperationException("Use Migration Centre for all opening imports. Legacy batches remain available for audit.");
        if (outstanding.Length == 0 || cheques.Length == 0 || outstanding.Length > 10_000_000 || cheques.Length > 10_000_000)
            throw new InvalidOperationException("Both files are required, each no larger than 10 MB.");
        if (cutover.Year < 1900 || asOf.Date != cutover.Date.AddDays(-1))
            throw new InvalidOperationException("Source date must be the day before cutover.");
        var hash = Hash($"{cutover:yyyy-MM-dd}|{asOf:yyyy-MM-dd}|{Convert.ToHexString(SHA256.HashData(outstanding))}|{Convert.ToHexString(SHA256.HashData(cheques))}");
        var batch = await db.CustomerMigrationBatches.FirstOrDefaultAsync(x => x.SourceHash == hash && x.Status != "Reversed", ct);
        if (batch == null)
        {
            batch = new CustomerMigrationBatch
            {
                CutoverDate = cutover.Date, SourceAsOfDate = asOf.Date, SourceHash = hash,
                OutstandingFileName = Path.GetFileName(outstandingName), ChequeFileName = Path.GetFileName(chequeName),
                OutstandingFile = outstanding, ChequeFile = cheques,
                SourceJson = JsonSerializer.Serialize(reader.Read(outstanding, cheques))
            };
            db.CustomerMigrationBatches.Add(batch);
            await db.SaveChangesAsync(ct);
        }
        return await ReviewAsync(batch.Id, null, ct);
    }

    public async Task<CustomerMigrationReview> ReviewAsync(int id, Dictionary<string, int>? bankOverrides, CancellationToken ct)
    {
        var batch = await Batch(id, ct);
        if (batch.Status != "Draft") return Stored(batch);
        var overrides = bankOverrides ?? (string.IsNullOrEmpty(batch.ReviewJson) ? [] : Stored(batch).BankOverrides);
        var review = await ValidateAsync(batch, overrides, ct);
        batch.ReviewHash = review.ReviewHash;
        batch.ReviewJson = JsonSerializer.Serialize(review);
        await db.SaveChangesAsync(ct);
        return review;
    }
    private async Task<CustomerMigrationReview> ValidateAsync(CustomerMigrationBatch batch, Dictionary<string, int> overrides, CancellationToken ct)
    {
        var source = JsonSerializer.Deserialize<CustomerMigrationSource>(batch.SourceJson) ?? throw new InvalidOperationException("Source is unreadable.");
        var result = new CustomerMigrationReview
        {
            BatchId = batch.Id, CutoverDate = batch.CutoverDate, SourceAsOfDate = batch.SourceAsOfDate,
            Status = batch.Status, Source = source, BankOverrides = overrides, Errors = [..source.Errors]
        };
        result.PostingEnabled = await db.SystemConfigs.AnyAsync(x => x.Key == EnableKey && x.Value == "true", ct);
        if (await db.CustomerMigrationBatches.AnyAsync(x => x.IsSignedOff && x.Status == "Posted", ct))
            result.Errors.Add("Customer migration is signed off. Further posting is disabled.");
        if (batch.SourceAsOfDate != batch.CutoverDate.AddDays(-1)) result.Errors.Add("Source date must be the day before cutover.");
        if (source.OutstandingReportDate != batch.SourceAsOfDate)
            result.Errors.Add($"Outstanding report date {source.OutstandingReportDate:yyyy-MM-dd} does not match source date {batch.SourceAsOfDate:yyyy-MM-dd}. Refresh the report.");
        if (source.ChequeReportDate != batch.SourceAsOfDate)
            result.Errors.Add($"Cheque report date {source.ChequeReportDate:yyyy-MM-dd} does not match source date {batch.SourceAsOfDate:yyyy-MM-dd}. Refresh the report.");
        if (!source.Outstanding.Any() || !source.Cheques.Any()) result.Errors.Add("Both reports must contain detail records.");
        if (!await db.FinancialPeriods.AnyAsync(p => p.StartDate <= batch.CutoverDate && p.EndDate >= batch.CutoverDate && !p.IsClosed, ct))
            result.Errors.Add("Cutover must fall in an open financial period.");
        if (await db.CustomerMigrationBatches.AnyAsync(x => x.Status == "Reversed" && x.CutoverDate == batch.CutoverDate && x.ReversedDate > batch.CutoverDate, ct))
            result.Errors.Add("An opening batch was reversed after this cutover date. A backdated replacement would double-count historical balances; use a reviewed adjustment instead.");
        var accounts = await db.Accounts.ToListAsync(ct);
        async Task<int> Resolve(string key, AccountType type)
        {
            var code = await db.SystemConfigs.Where(x => x.Key == key).Select(x => x.Value).FirstOrDefaultAsync(ct);
            var match = accounts.SingleOrDefault(x => x.Code == code && x.Type == type && x.IsTransactionAccount && x.IsActive);
            if (match == null) result.Errors.Add($"Missing valid account mapping: {key}.");
            return match?.Id ?? 0;
        }
        result.OpeningBalanceAccountId = await Resolve("Account.Equity.OpeningBalance", AccountType.Equity);
        result.UndepositedAccountId = await Resolve("Account.Asset.UndepositedFunds", AccountType.Asset);
        var codes = source.Outstanding.Select(x => x.Code).Concat(source.Cheques.Select(x => x.Code)).Distinct().Order().ToArray();
        var customers = await db.Customers.ToListAsync(ct);
        foreach (var code in codes)
        {
            var matches = customers.Where(c => string.Equals(c.LegacyCustomerCode, code, StringComparison.OrdinalIgnoreCase)).ToList();
            if (code.Length is < 1 or > 50 || matches.Count != 1) { result.Errors.Add($"Customer {code}: exactly one legacy-code match is required."); continue; }
            var customer = matches[0];
            var account = accounts.FirstOrDefault(a => a.Id == customer.DefaultReceivableAccountId && a.Type == AccountType.Asset && a.IsTransactionAccount && a.IsActive);
            if (!customer.IsActive || customer.CurrencyCode != "LKR" || account == null)
                result.Errors.Add($"Customer {code}: active LKR customer with a valid receivable account is required.");
            if (new[] { customer.Name, customer.ContactPerson, customer.Email, customer.Phone, customer.AddressLine1, customer.Province, customer.District, customer.City }.Any(string.IsNullOrWhiteSpace))
                result.Errors.Add($"Customer {code}: complete the required master fields before migration.");
            var masterValidation = new List<global::System.ComponentModel.DataAnnotations.ValidationResult>();
            global::System.ComponentModel.DataAnnotations.Validator.TryValidateObject(customer, new(customer), masterValidation, true);
            if (masterValidation.Count > 0) result.Errors.Add($"Customer {code}: required master fields contain invalid values.");
            if (customer.DefaultReceivableAccountId == result.UndepositedAccountId)
                result.Errors.Add($"Customer {code}: AR must be separate from Undeposited Funds.");
            result.Customers.Add(new(code, customer.Id, customer.Name, customer.DefaultReceivableAccountId));
        }
        foreach (var r in source.Outstanding)
        {
            if (string.IsNullOrWhiteSpace(r.Reference) || r.Reference.Length > 100 || r.Date > batch.SourceAsOfDate || r.Date.Year < 1900 || r.Amount == 0)
                result.Errors.Add($"Outstanding row {r.Row}: reference, nonzero amount and document date through source date are required.");
        }
        foreach (var group in source.Outstanding.GroupBy(x => x.Reference, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            result.Errors.Add($"Duplicate outstanding reference: {group.Key}.");
        foreach (var group in source.Cheques.GroupBy(x => x.Reference, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            result.Errors.Add($"Duplicate cheque receipt reference: {group.Key}.");
        foreach (var group in source.Cheques.GroupBy(x => $"{x.BankCode}|{x.ChequeNumber}", StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            result.Errors.Add($"Duplicate bank/cheque identity: {group.Key}.");
        var branches = await db.BankBranches.Include(x => x.Bank).ToListAsync(ct);
        foreach (var code in source.Cheques.Select(x => x.BankCode).Distinct())
        {
            var matches = overrides.TryGetValue(code, out var branchId)
                ? branches.Where(x => x.Id == branchId).ToList()
                : branches.Where(x => $"{x.Bank.BankCode}-{x.BranchCode}" == code).ToList();
            if (matches.Count != 1 || (!matches[0].Bank.IsActive || !matches[0].IsActive))
                result.Errors.Add($"Bank/branch {code}: select a verified existing branch.");
            else result.Banks.Add(new(code, matches[0].Id, $"{matches[0].Bank.Name} / {matches[0].BranchName}"));
        }
        foreach (var r in source.Cheques)
        {
            if (r.Amount <= 0 || string.IsNullOrWhiteSpace(r.ChequeNumber) || r.ChequeNumber.Length > 100 || r.Reference.Length > 100 || r.ReceivedDate > batch.SourceAsOfDate || r.ReceivedDate.Year < 1900 || r.ChequeDate.Year < 1900)
                result.Errors.Add($"Cheque row {r.Row}: positive amount, text number and valid receipt date through source date are required.");
            if (r.ChequeDate < batch.CutoverDate) result.Warnings.Add($"Cheque {r.Reference}: matured before cutover; verify it is physically in hand.");
        }
        var customerIds = result.Customers.Select(x => x.CustomerId).ToList();
        var arAccounts = result.Customers.Select(x => x.ReceivableAccountId).Append(result.UndepositedAccountId).ToList();
        if (await db.SalesInvoices.AnyAsync(x => customerIds.Contains(x.CustomerId) && x.IsPosted, ct)
            || await db.PaymentTransactions.AnyAsync(x => x.CustomerId.HasValue && customerIds.Contains(x.CustomerId.Value), ct)
            || await db.CreditNotes.AnyAsync(x => customerIds.Contains(x.CustomerId) && x.IsPosted, ct)
            || await db.CustomerDebitMemos.AnyAsync(x => customerIds.Contains(x.CustomerId), ct)
            || await db.ChequeRegisters.AnyAsync(x => customerIds.Contains(x.CustomerId) && (!x.MigrationBatchId.HasValue || x.MigrationBatch!.Status != "Reversed"), ct)
            || await db.CustomerOpeningItems.AnyAsync(x => customerIds.Contains(x.CustomerId) && x.MigrationBatch.Status != "Reversed", ct))
            result.Errors.Add("Customer financial records already exist. Reconcile them before loading a clean opening position.");
        if (accounts.Any(x => arAccounts.Contains(x.Id) && x.Balance != 0)
            || await db.JournalLines.AnyAsync(x => arAccounts.Contains(x.AccountId) && x.JournalEntry.Module != "CustomerMigration", ct))
            result.Errors.Add("Receivable/Undeposited control accounts must have zero balances before this opening import.");
        foreach (var code in codes)
        {
            var ar = source.Outstanding.Where(x => x.Code == code).ToList();
            var name = ar.FirstOrDefault()?.Name ?? source.Cheques.First(x => x.Code == code).Name;
            result.Totals.Add(new(code, name, ar.Where(x => x.Amount > 0).Sum(x => x.Amount),
                -ar.Where(x => x.Amount < 0).Sum(x => x.Amount), ar.Sum(x => x.Amount), source.Cheques.Where(x => x.Code == code).Sum(x => x.Amount)));
        }
        foreach (var mapping in result.Customers.GroupBy(x => x.ReceivableAccountId))
        {
            var sum = source.Outstanding.Where(x => mapping.Any(m => m.Code == x.Code)).Sum(x => x.Amount);
            if (sum != 0) result.Journal.Add(new(mapping.Key, accounts.FirstOrDefault(x => x.Id == mapping.Key)?.Name ?? "Unmapped AR", Math.Max(sum, 0), Math.Max(-sum, 0)));
        }
        var chequeTotal = source.Cheques.Sum(x => x.Amount);
        result.Journal.Add(new(result.UndepositedAccountId, "Undeposited Funds", chequeTotal, 0));
        var net = source.Outstanding.Sum(x => x.Amount) + chequeTotal;
        result.Journal.Add(new(result.OpeningBalanceAccountId, "Opening Balance Equity", Math.Max(-net, 0), Math.Max(net, 0)));
        result.ReviewHash = Hash(JsonSerializer.Serialize(new { batch.SourceHash, result.Customers, result.Banks, result.Journal, result.Errors, result.PostingEnabled }));
        return result;
    }

    public async Task<CustomerMigrationReview> PostAsync(int id, string hash, bool physicallyVerified, CancellationToken ct)
    {
        if (db.MigrationProjects != null && await db.MigrationProjects.AnyAsync(ct)) throw new InvalidOperationException("Use Migration Centre to validate, approve and post opening balances.");
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var batch = await Batch(id, ct);
        if (batch.Status == "Posted") return Stored(batch);
        if (batch.Status != "Draft") throw new InvalidOperationException("Only a draft can be posted.");
        var review = await ValidateAsync(batch, Stored(batch).BankOverrides, ct);
        if (!review.CanPost) throw new InvalidOperationException(string.Join("\n", review.Errors.Append(review.PostingEnabled ? "" : $"Enable {EnableKey} after UAT approval.").Where(x => x != "")));
        if (!physicallyVerified) throw new InvalidOperationException("Confirm refreshed source totals and physical cheque verification.");
        if (hash != batch.ReviewHash || hash != review.ReviewHash) throw new InvalidOperationException("Source or setup changed. Preview again.");
        var customers = review.Customers.ToDictionary(x => x.Code);
        var banks = review.Banks.ToDictionary(x => x.Code);
        foreach (var row in review.Source.Outstanding)
        {
            var customer = customers[row.Code];
            db.CustomerOpeningItems.Add(new CustomerOpeningItem
            {
                MigrationBatchId = id, CustomerId = customer.CustomerId, LegacyCustomerCode = row.Code,
                ReceivableAccountId = customer.ReceivableAccountId, ReferenceNo = row.Reference, Kind = row.Kind,
                DocumentDate = row.Date, PostingDate = batch.CutoverDate, DueDate = row.Amount > 0 ? row.Date.AddDays(30) : null,
                Amount = row.Amount, SourceDebitAmount = row.DebitAmount, SourceRow = row.Row, Notes = row.Notes
            });
        }
        foreach (var row in review.Source.Cheques)
        {
            db.ChequeRegisters.Add(new ChequeRegister
            {
                MigrationBatchId = id, CustomerId = customers[row.Code].CustomerId,
                OpeningReceivableAccountId = customers[row.Code].ReceivableAccountId, OpeningUndepositedAccountId = review.UndepositedAccountId,
                BankBranchId = banks[row.BankCode].BankBranchId, ChequeNumber = row.ChequeNumber,
                ChequeDate = row.ChequeDate, ReceivedDate = row.ReceivedDate, Amount = row.Amount,
                LegacyReceiptReference = row.Reference, LegacyDocumentReferences = row.DocumentReferences,
                OpeningSourceRow = row.Row, Status = ChequeStatus.InSafe
            });
        }
        var journal = await journals.PostJournalAsync(new JournalEntryRequest
        {
            Date = batch.CutoverDate, Module = "CustomerMigration", ReferenceNo = $"MIG-CUSTOMER-{id}",
            Description = "Customer opening receivables, return credits and cheques in hand",
            Lines = review.Journal.Select(x => new JournalLineRequest { AccountId = x.AccountId, Debit = x.Debit, Credit = x.Credit, Note = $"Customer migration batch {id}" }).ToList()
        });
        if (!journal.Succeeded) throw new InvalidOperationException(journal.Message);
        batch.JournalEntryId = journal.Data; batch.PostedAt = DateTime.UtcNow; batch.Status = "Posted";
        review.Status = "Posted"; batch.ReviewJson = JsonSerializer.Serialize(review);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return review;
    }
    public async Task<CustomerMigrationReview> GetAsync(int id, CancellationToken ct) => Stored(await Batch(id, ct));
    public async Task<object> ReconcileAsync(int id, CancellationToken ct)
    {
        var batch = await Batch(id, ct); var review = Stored(batch);
        var items = await db.CustomerOpeningItems.Where(x => x.MigrationBatchId == id).ToListAsync(ct);
        var cheques = await db.ChequeRegisters.Where(x => x.MigrationBatchId == id).ToListAsync(ct);
        var journal = batch.JournalEntryId.HasValue ? await db.JournalLines.Where(x => x.JournalEntryId == batch.JournalEntryId.Value).ToListAsync(ct) : [];
        var rows = review.Totals.Select(x => new
        {
            x.Code, x.Name, SourceAr = x.NetAr, ImportedAr = items.Where(i => i.LegacyCustomerCode == x.Code).Sum(i => i.Amount),
            SourceCheques = x.Cheques, ImportedCheques = cheques.Where(c => review.Customers.Any(m => m.Code == x.Code && m.CustomerId == c.CustomerId)).Sum(c => c.Amount)
        }).ToList();
        return new { Batch = review, Customers = rows, OpeningItemCount = items.Count, ChequeCount = cheques.Count,
            JournalBalances = journal.Sum(x => x.Debit) == journal.Sum(x => x.Credit),
            Matches = batch.Status == "Posted" && items.Count == review.Source.Outstanding.Count && cheques.Count == review.Source.Cheques.Count
                && rows.All(x => x.SourceAr == x.ImportedAr && x.SourceCheques == x.ImportedCheques)
                && journal.Sum(x => x.Debit) == journal.Sum(x => x.Credit)
                && journal.Select(x => x.AccountId).Distinct().Order().SequenceEqual(review.Journal.Select(x => x.AccountId).Distinct().Order())
                && review.Journal.All(x => journal.Where(l => l.AccountId == x.AccountId).Sum(l => l.Debit - l.Credit) == x.Debit - x.Credit) };
    }
    public async Task ReverseAsync(int id, DateTime date, string reason, CancellationToken ct)
    {
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var batch = await Batch(id, ct);
        if (batch.Status != "Posted" || batch.IsSignedOff || string.IsNullOrWhiteSpace(reason) || date.Date < batch.CutoverDate)
            throw new InvalidOperationException("Unsigned posted batch, reversal reason and date on/after cutover are required.");
        var itemIds = await db.CustomerOpeningItems.Where(x => x.MigrationBatchId == id).Select(x => x.Id).ToListAsync(ct);
        var customerIds = Stored(batch).Customers.Select(x => x.CustomerId).ToList();
        if (await db.PaymentTransactions.AnyAsync(x => x.CustomerId.HasValue && customerIds.Contains(x.CustomerId.Value), ct)
            || await db.SalesInvoices.AnyAsync(x => customerIds.Contains(x.CustomerId) && x.IsPosted, ct)
            || await db.CreditNotes.AnyAsync(x => customerIds.Contains(x.CustomerId) && x.IsPosted, ct)
            || await db.CustomerCreditApplications.AnyAsync(x => itemIds.Contains(x.CreditOpeningItemId), ct)
            || await db.ChequeRegisters.AnyAsync(x => x.MigrationBatchId == id && x.Status != ChequeStatus.InSafe, ct)
            || await db.CustomerDebitMemos.AnyAsync(x => customerIds.Contains(x.CustomerId), ct))
            throw new InvalidOperationException("Downstream financial activity exists. Batch reversal is blocked; use targeted corrections.");
        var lines = await db.JournalLines.Where(x => x.JournalEntryId == batch.JournalEntryId).ToListAsync(ct);
        var reversed = await journals.PostJournalAsync(new JournalEntryRequest
        {
            Date = date.Date, Module = "CustomerMigration", ReferenceNo = $"MIG-CUSTOMER-{id}-REV",
            Description = reason, Lines = lines.Select(x => new JournalLineRequest { AccountId = x.AccountId, Debit = x.Credit, Credit = x.Debit, Note = reason }).ToList()
        });
        if (!reversed.Succeeded) throw new InvalidOperationException(reversed.Message);
        batch.Status = "Reversed"; batch.ReversedDate = date.Date; batch.ReversalReason = reason; batch.ReversalJournalEntryId = reversed.Data;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task SignOffAsync(int id, CancellationToken ct)
    {
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var batch = await Batch(id, ct);
        if (batch.Status != "Posted") throw new InvalidOperationException("Only posted batches can be signed off.");
        var reconciliation = JsonSerializer.SerializeToElement(await ReconcileAsync(id, ct));
        if (!reconciliation.GetProperty("Matches").GetBoolean()) throw new InvalidOperationException("Reconciliation must match before sign-off.");
        var setting = await db.SystemConfigs.FirstOrDefaultAsync(x => x.Key == EnableKey, ct);
        if (setting != null) setting.Value = "false";
        batch.IsSignedOff = true; batch.SignedOffAt = DateTime.UtcNow; batch.SignedOffBy = currentUser.UserId;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task<CustomerMigrationBatch> Batch(int id, CancellationToken ct) =>
        await db.CustomerMigrationBatches.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new InvalidOperationException("Migration batch not found.");
    private static CustomerMigrationReview Stored(CustomerMigrationBatch batch)
    {
        var review = JsonSerializer.Deserialize<CustomerMigrationReview>(batch.ReviewJson) ?? throw new InvalidOperationException("Preview is missing.");
        review.Status = batch.Status; review.IsSignedOff = batch.IsSignedOff; return review;
    }
}
