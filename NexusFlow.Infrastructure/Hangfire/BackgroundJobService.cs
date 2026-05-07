using Hangfire;
using NexusFlow.AppCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace NexusFlow.Infrastructure.Hangfire
{
    public sealed class BackgroundJobService : IBackgroundJobService
    {
        private readonly IBackgroundJobClient _client;
        private readonly IRecurringJobManager _recurringJobManager;

        public BackgroundJobService(
            IBackgroundJobClient client,
            IRecurringJobManager recurringJobManager)
        {
            _client = client;
            _recurringJobManager = recurringJobManager;
        }

        public string Enqueue<T>(Expression<Action<T>> methodCall)
            => _client.Enqueue(methodCall);

        public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay)
            => _client.Schedule(methodCall, delay);

        public void AddOrUpdateRecurring<T>(
            string jobId,
            Expression<Action<T>> methodCall,
            string cronExpression,
            string queue = "default")
            => _recurringJobManager.AddOrUpdate(jobId, methodCall, cronExpression,
                new RecurringJobOptions { QueueName = queue });

        public void RemoveRecurring(string jobId)
            => _recurringJobManager.RemoveIfExists(jobId);
    }
}
