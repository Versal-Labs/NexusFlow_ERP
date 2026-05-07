using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IBackgroundJobService
    {
        /// <summary>Fire and forget — runs once immediately.</summary>
        string Enqueue<T>(System.Linq.Expressions.Expression<Action<T>> methodCall);

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
}
