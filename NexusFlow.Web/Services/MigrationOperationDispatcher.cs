using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Features.Migration;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Web.Services;

public sealed class MigrationOperationDispatcher(IServiceScopeFactory scopes, ILogger<MigrationOperationDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
                var ids = await db.MigrationOperations.AsNoTracking().Where(x => x.Status == "Queued" && x.JobId == null).Select(x => x.Id).Take(100).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    using var dispatch = scopes.CreateScope();
                    await dispatch.ServiceProvider.GetRequiredService<MigrationCentreService>().DispatchOperationAsync(id, false, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Migration dispatch check could not complete; durable queued operations retained."); }
        }
    }
}
