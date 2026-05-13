using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using NexusFlow.AppCore.Jobs.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Jobs.Runners
{
    [AutomaticRetry(
    Attempts = 2,                                    // Payroll is critical — fewer blind retries
    DelaysInSeconds = new[] { 600, 1800 },           // 10min, 30min backoff
    OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [Queue("critical")]                                  // Payroll goes on critical queue
    public sealed class GenerateDraftPayrollJobRunner
    {
        private readonly IGenerateDraftPayrollJob _job;
        private readonly ILogger<GenerateDraftPayrollJobRunner> _logger;

        public GenerateDraftPayrollJobRunner(
            IGenerateDraftPayrollJob job,
            ILogger<GenerateDraftPayrollJobRunner> logger)
        {
            _job = job;
            _logger = logger;
        }

        public async Task RunAsync(
            PerformContext context,
            int? year,
            int? month,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var targetYear = year ?? now.Year;
            var targetMonth = month ?? now.Month;
            var monthYearStr = $"{targetYear}-{targetMonth:D2}";

            context.WriteLine($"[{DateTimeOffset.UtcNow:u}] ⏳ Starting Draft Payroll generation for {monthYearStr}");
            context.WriteLine($"Job ID     : {context.BackgroundJob.Id}");
            context.WriteLine($"Target     : Year={targetYear}, Month={targetMonth}");
            context.WriteLine($"Triggered  : {(year.HasValue ? "Manually" : "Scheduled")}");
            context.WriteLine(new string('-', 60));

            try
            {
                await _job.ExecuteAsync(targetYear, targetMonth, cancellationToken);

                context.WriteLine(new string('-', 60));
                context.WriteLine($"[{DateTimeOffset.UtcNow:u}] ✅ Draft Payroll generated successfully for {monthYearStr}");
            }
            catch (Exception ex)
            {
                context.WriteLine(new string('-', 60));
                context.WriteLine($"[{DateTimeOffset.UtcNow:u}] ❌ FAILED for {monthYearStr}");
                context.WriteLine($"Error: {ex.Message}");

                _logger.LogError(ex,
                    "GenerateDraftPayrollJob failed for {MonthYear}. JobId: {JobId}",
                    monthYearStr,
                    context.BackgroundJob.Id);

                throw; // Let Hangfire handle retry/fail
            }
        }
    }
}
