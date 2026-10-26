using Microsoft.Extensions.DependencyInjection;

namespace NexusFlow.AppCore.Features.Migration;

public sealed class MigrationOperationJob(IServiceScopeFactory scopes)
{
    public async Task RunAsync(int operationId)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MigrationCentreService>().RunOperationAsync(operationId, CancellationToken.None);
    }
}
