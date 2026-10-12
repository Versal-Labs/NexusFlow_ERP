using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Features.Sales.Customers.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Features.Finance.CustomerMigration;

public sealed record CustomerMasterPreview(List<SaveCustomerCommand> Customers, List<string> Errors, string PreviewHash);
public sealed class CustomerMasterImportService(IErpDbContext db)
{
    public const string Headers = "Id,LegacyCustomerCode,Name,TaxRegNo,CustomerGroupId,PriceLevelId,SalesRepId,ContactPerson,Email,Phone,AddressLine1,Province,District,City,Country,DefaultReceivableAccountId,DefaultRevenueAccountId,PaymentTermId,CreditLimit,CreditPeriodDays,BankId,BankBranchId,BankAccountNumber";
    public async Task<CustomerMasterPreview> PreviewAsync(byte[] bytes, CancellationToken ct)
    {
        var rows = new List<SaveCustomerCommand>(); var errors = new List<string>();
        try
        {
            using var stream = new MemoryStream(bytes); using var reader = new StreamReader(stream, Encoding.UTF8, true);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            if (!await csv.ReadAsync()) throw new InvalidDataException("CSV has no header."); csv.ReadHeader();
            foreach (var column in Headers.Split(','))
                if (!csv.HeaderRecord!.Contains(column)) errors.Add($"Missing CSV column {column}.");
            if (errors.Count == 0)
                while (await csv.ReadAsync())
                {
                    if (rows.Count == 20000) { errors.Add("Maximum 20,000 customers."); break; }
                    var row = csv.GetRecord<SaveCustomerCommand>();
                    row.LegacyCustomerCode = row.LegacyCustomerCode?.Trim().ToUpperInvariant(); row.Name = row.Name.Trim(); rows.Add(row);
                }
        }
        catch (Exception ex) when (ex is CsvHelperException or FormatException or OverflowException or InvalidDataException)
        { errors.Add("Invalid CSV. Use the template column names, integer IDs, ISO text and decimal amounts."); }
        if (rows.Count == 0) errors.Add("No customers to import.");
        var existing = await db.Customers.ToListAsync(ct);
        var accounts = await db.Accounts.ToListAsync(ct);
        var groups = await db.SystemLookups.Where(x => x.Type == "CustomerGroup" && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        var prices = await db.SystemLookups.Where(x => x.Type == "PriceLevel" && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        var terms = await db.SystemLookups.Where(x => x.Type == "PaymentTerm" && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        var reps = await db.Employees.Where(x => x.IsSalesRep).Select(x => x.Id).ToListAsync(ct);
        var banks = await db.Banks.ToListAsync(ct); var branches = await db.BankBranches.ToListAsync(ct);
        var lockedIds = await db.CustomerOpeningItems.Where(x => x.MigrationBatch.Status == "Posted").Select(x => x.CustomerId).ToListAsync(ct);
        lockedIds.AddRange(await db.ChequeRegisters.Where(x => x.MigrationBatchId != null && x.MigrationBatch!.Status == "Posted").Select(x => x.CustomerId).ToListAsync(ct));
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i]; var prefix = $"CSV row {i + 2}: ";
            var validation = new List<ValidationResult>();
            Validator.TryValidateObject(r, new ValidationContext(r), validation, true);
            errors.AddRange(validation.Select(x => prefix + x.ErrorMessage));
            if (string.IsNullOrWhiteSpace(r.LegacyCustomerCode) || r.LegacyCustomerCode.Length > 50) errors.Add(prefix + "Legacy code is required, up to 50 characters.");
            var current = existing.FirstOrDefault(x => x.Id == r.Id);
            if (r.Id < 0 || (r.Id > 0 && current == null)) errors.Add(prefix + "Existing Id must identify a customer; use 0 to create.");
            if (existing.Any(x => x.Id != r.Id && string.Equals(x.LegacyCustomerCode, r.LegacyCustomerCode, StringComparison.OrdinalIgnoreCase))) errors.Add(prefix + "Legacy code already belongs to another customer. Supply its explicit Id to update it.");
            if (existing.Any(x => x.Id != r.Id && string.Equals(x.Name, r.Name, StringComparison.OrdinalIgnoreCase))) errors.Add(prefix + "Name already belongs to another customer.");
            if (current != null && lockedIds.Contains(r.Id) && (current.LegacyCustomerCode != r.LegacyCustomerCode || current.DefaultReceivableAccountId != r.DefaultReceivableAccountId)) errors.Add(prefix + "Legacy code and AR account are locked after migration.");
            if (!groups.Contains(r.CustomerGroupId) || !prices.Contains(r.PriceLevelId) || !terms.Contains(r.PaymentTermId) || (r.SalesRepId.HasValue && !reps.Contains(r.SalesRepId.Value))) errors.Add(prefix + "Choose valid group, price level, payment term and optional sales rep IDs.");
            if (!accounts.Any(x => x.Id == r.DefaultReceivableAccountId && x.IsTransactionAccount && x.IsActive && x.Type == AccountType.Asset)
                || !accounts.Any(x => x.Id == r.DefaultRevenueAccountId && x.IsTransactionAccount && x.IsActive && x.Type == AccountType.Revenue)) errors.Add(prefix + "Valid AR Asset and Revenue transaction account IDs are required.");
            if ((r.BankId.HasValue && !banks.Any(x => x.Id == r.BankId && x.IsActive)) || (r.BankBranchId.HasValue && !branches.Any(x => x.Id == r.BankBranchId && x.BankId == r.BankId && x.IsActive))) errors.Add(prefix + "Bank/branch mapping is invalid.");
            if (r.CreditLimit < 0 || r.CreditPeriodDays < 0 || Math.Round(r.CreditLimit, 2) != r.CreditLimit) errors.Add(prefix + "Credit settings are invalid.");
            if (r.Name.Length > 200 || r.TaxRegNo.Length > 50 || r.ContactPerson.Length > 100 || r.Email.Length > 150 || r.Phone.Length > 50 || r.AddressLine1.Length > 200
                || new[] { r.Province, r.District, r.City }.Any(x => x.Length > 100) || r.Country.Length > 50 || r.BankAccountNumber.Length > 50) errors.Add(prefix + "One or more master fields exceeds its maximum length.");
        }
        if (rows.GroupBy(x => x.LegacyCustomerCode, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)
            || rows.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)
            || rows.Where(x => x.Id > 0).GroupBy(x => x.Id).Any(x => x.Count() > 1)) errors.Add("Duplicate legacy code, customer name or update Id in the CSV.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { rows, errors,
            Existing = existing.Select(x => new { x.Id, x.LegacyCustomerCode, x.Name, x.DefaultReceivableAccountId, x.LastModifiedAt }), lockedIds }))));
        return new(rows, errors.Distinct().ToList(), hash);
    }
    public async Task<CustomerMasterPreview> ExecuteAsync(byte[] bytes, string hash, CancellationToken ct)
    {
        if (db.MigrationProjects != null && await db.MigrationProjects.AnyAsync(ct)) throw new InvalidOperationException("Use Administration > Migration Centre to import migration masters.");
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var preview = await PreviewAsync(bytes, ct);
        if (preview.Errors.Count != 0 || preview.PreviewHash != hash) throw new InvalidOperationException("CSV or master setup changed. Preview and resolve errors before importing.");
        var handler = new SaveCustomerHandler(db);
        foreach (var row in preview.Customers)
        {
            var result = await handler.Handle(row, ct);
            if (!result.Succeeded) throw new InvalidOperationException(result.Message);
        }
        await tx.CommitAsync(ct); return preview;
    }
}
