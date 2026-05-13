namespace NexusFlow.AppCore.Jobs.Interfaces
{
    public interface IGenerateDraftPayrollJob
    {
        Task ExecuteAsync(int year, int month, CancellationToken cancellationToken = default);
    }
}