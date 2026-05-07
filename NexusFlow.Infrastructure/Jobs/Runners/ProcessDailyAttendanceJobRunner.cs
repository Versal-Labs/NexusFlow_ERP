using Hangfire;
using Hangfire.Server;
using Hangfire.Console;
using Microsoft.Extensions.Logging;
using NexusFlow.AppCore.Jobs.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Jobs.Runners
{
    [AutomaticRetry(
    Attempts = 3,
    DelaysInSeconds = new[] { 300, 600, 1800 }, // 5min, 10min, 30min backoff
    OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [Queue("default")]
    public sealed class ProcessDailyAttendanceJobRunner
    {
        private readonly IProcessDailyAttendanceJob _job;
        private readonly ILogger<ProcessDailyAttendanceJobRunner> _logger;

        public ProcessDailyAttendanceJobRunner(
            IProcessDailyAttendanceJob job,
            ILogger<ProcessDailyAttendanceJobRunner> logger)
        {
            _job = job;
            _logger = logger;
        }

        public async Task RunAsync(
            PerformContext context,            // ← Hangfire injects this for dashboard logging
            DateTime? targetDate,
            CancellationToken cancellationToken)
        {
            var dateLabel = (targetDate ?? DateTime.UtcNow.AddDays(-1)).Date.ToString("yyyy-MM-dd");

            context.WriteLine($"[{DateTimeOffset.UtcNow:u}] ⏳ Starting attendance processing for {dateLabel}");
            context.WriteLine($"Job ID: {context.BackgroundJob.Id}");

            try
            {
                await _job.ExecuteAsync(targetDate, cancellationToken);

                context.WriteLine($"[{DateTimeOffset.UtcNow:u}] ✅ Attendance processing completed for {dateLabel}");
            }
            catch (Exception ex)
            {
                context.WriteLine($"[{DateTimeOffset.UtcNow:u}] ❌ FAILED: {ex.Message}");
                _logger.LogError(ex, "ProcessDailyAttendanceJob failed for date {Date}", dateLabel);
                throw; // Re-throw so Hangfire marks it as failed and retries
            }
        }
    }
}
