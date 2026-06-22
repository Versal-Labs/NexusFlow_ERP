namespace NexusFlow.AppCore.Interfaces
{
    public interface IFinancialPeriodControlledRequest
    {
        DateTime FinancialDate { get; }
    }

    public interface IFinancialPeriodService
    {
        Task<FinancialPeriodStatusDto> GetStatusAsync(DateTime date, CancellationToken cancellationToken = default);
    }

    public sealed class FinancialPeriodStatusDto
    {
        public DateTime BusinessDate { get; init; }
        public string Status { get; init; } = "Missing";
        public int? PeriodId { get; init; }
        public int? Year { get; init; }
        public int? Month { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public bool CanCreateTransactions => Status == "Open";
        public string Message { get; init; } = string.Empty;
    }
}
