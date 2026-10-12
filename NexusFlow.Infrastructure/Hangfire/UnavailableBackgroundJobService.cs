using System.Linq.Expressions;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Hangfire;

public sealed class UnavailableBackgroundJobService : IBackgroundJobService
{
    private static InvalidOperationException Unavailable() => new(
        "Background jobs are unavailable until installation or upgrade is complete and the application is restarted.");

    public string Enqueue<T>(Expression<Action<T>> methodCall) => throw Unavailable();

    public string EnqueueAsync<T>(Expression<Func<T, Task>> methodCall) => throw Unavailable();

    public string? GetState(string jobId) => throw Unavailable();

    public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) => throw Unavailable();

    public void AddOrUpdateRecurring<T>(string jobId, Expression<Action<T>> methodCall,
        string cronExpression, string queue = "default") => throw Unavailable();

    public void RemoveRecurring(string jobId) => throw Unavailable();
}
