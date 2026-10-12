using Microsoft.Extensions.DependencyInjection;

namespace NexusFlow.AppCore.Features.Migration;

public sealed class MigrationExecutionJob(IServiceScopeFactory scopes)
{
    public async Task RunAsync(int runId)
    {
        try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().ExecuteRunAsync(runId, CancellationToken.None); }
        catch (Exception ex)
        {
            using var clean = scopes.CreateScope(); await clean.ServiceProvider.GetRequiredService<MigrationCentreService>().MarkRunFailedAsync(runId, ex.Message, CancellationToken.None);
            throw;
        }
    }
}
