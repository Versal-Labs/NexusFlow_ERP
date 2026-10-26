using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IBackgroundJobService
    {
        /// <summary>Fire and forget — runs once immediately.</summary>
        string Enqueue<T>(System.Linq.Expressions.Expression<Action<T>> methodCall);
        string EnqueueAsync<T>(System.Linq.Expressions.Expression<Func<T, Task>> methodCall)
            => throw new NotSupportedException("Async background jobs are not configured.");
        string? GetState(string jobId) => null;
        // Null from an older implementation is uncertain, never proof that a worker stopped.
        BackgroundJobState LookupState(string jobId)
        {
            try { return GetState(jobId) is { } state ? BackgroundJobState.FromName(state) : new(BackgroundJobStatus.Unavailable); }
            catch { return new(BackgroundJobStatus.Unavailable); }
        }

        /// <summary>Delayed job — runs once after a delay.</summary>
        string Schedule<T>(System.Linq.Expressions.Expression<Action<T>> methodCall, TimeSpan delay);

        /// <summary>Recurring job — runs on a CRON schedule.</summary>
        void AddOrUpdateRecurring<T>(
            string jobId,
            System.Linq.Expressions.Expression<Action<T>> methodCall,
            string cronExpression,
            string queue = "default");

        void RemoveRecurring(string jobId);
    }
    public enum BackgroundJobStatus { Active, Stopped, Missing, Unavailable }
    public sealed record BackgroundJobState(BackgroundJobStatus Status, string? Name = null)
    {
        public bool ConfirmedInactive => Status is BackgroundJobStatus.Stopped or BackgroundJobStatus.Missing;
        public static BackgroundJobState FromName(string name) => new(name is "Failed" or "Succeeded" or "Deleted" ? BackgroundJobStatus.Stopped : BackgroundJobStatus.Active, name);
    }
}
