using NexusFlow.Domain.Entities.Finance;

namespace NexusFlow.AppCore.Features.Finance.CustomerMigration;

public sealed record OutstandingSourceRow(int Row, string Code, string Name, string Reference, DateTime Date,
    string Kind, decimal Amount, decimal DebitAmount, string Notes);
public sealed record ChequeSourceRow(int Row, string Code, string Name, string Reference, string DocumentReferences,
    string BankCode, string ChequeNumber, DateTime ChequeDate, DateTime ReceivedDate, decimal Amount);
public sealed class CustomerMigrationSource
{
    public List<OutstandingSourceRow> Outstanding { get; set; } = [];
    public List<ChequeSourceRow> Cheques { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public DateTime? OutstandingReportDate { get; set; }
    public DateTime? ChequeReportDate { get; set; }
}
public sealed record MigrationCustomerMapping(string Code, int CustomerId, string CustomerName, int ReceivableAccountId);
public sealed record MigrationBankMapping(string Code, int BankBranchId, string Label);
public sealed record MigrationJournalLine(int AccountId, string AccountName, decimal Debit, decimal Credit);
public sealed record MigrationCustomerTotal(string Code, string Name, decimal Debit, decimal Credit, decimal NetAr, decimal Cheques);
public sealed class CustomerMigrationReview
{
    public int BatchId { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CutoverDate { get; set; }
    public DateTime SourceAsOfDate { get; set; }
    public string ReviewHash { get; set; } = "";
    public CustomerMigrationSource Source { get; set; } = new();
    public List<MigrationCustomerMapping> Customers { get; set; } = [];
    public List<MigrationBankMapping> Banks { get; set; } = [];
    public Dictionary<string, int> BankOverrides { get; set; } = [];
    public List<MigrationCustomerTotal> Totals { get; set; } = [];
    public List<MigrationJournalLine> Journal { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int OpeningBalanceAccountId { get; set; }
    public int UndepositedAccountId { get; set; }
    public bool PostingEnabled { get; set; }
    public bool IsSignedOff { get; set; }
    public bool CanPost => Status == "Draft" && Errors.Count == 0 && PostingEnabled;
}
public interface ICustomerMigrationFileReader
{
    CustomerMigrationSource Read(byte[] outstanding, byte[] cheques);
}
