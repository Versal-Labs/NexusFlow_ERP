using Hangfire;
using Hangfire.Common;
using NexusFlow.AppCore.Features.Migration;

namespace NexusFlow.Infrastructure.Hangfire;

/// <summary>Financial checkpoints resume explicitly; automatic retries must not race operator resume.</summary>
public sealed class MigrationJobFilterProvider : IJobFilterProvider
{
    public IEnumerable<JobFilter> GetFilters(Job job)
    {
        if (job.Type == typeof(MigrationExecutionJob)) yield return new JobFilter(new AutomaticRetryAttribute { Attempts = 0 }, JobFilterScope.Type, null);
    }
}
