using Hangfire;
using NexusFlow.Infrastructure.Jobs.Runners;
using System;
using System.Collections.Generic;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace NexusFlow.Infrastructure.Hangfire
{
    public static class RecurringJobsRegistrar
    {
        public static void RegisterAll()
        {
            // Runs every day at 00:30 AM UTC
            // (30 mins after midnight gives devices time to sync last punches)
            RecurringJob.AddOrUpdate<ProcessDailyAttendanceJobRunner>(
                recurringJobId: "daily-attendance-processor",
                methodCall: runner => runner.RunAsync(null!, null, CancellationToken.None),
                cronExpression: "0 1 * * *",  
                options: new RecurringJobOptions
                {
                    QueueName = "default",
                    TimeZone = TimeZoneInfo.Utc
                });

            // Payroll — 25th of every month at 02:00 UTC
            RecurringJob.AddOrUpdate<GenerateDraftPayrollJobRunner>(
                recurringJobId: "monthly-payroll-generator",
                methodCall: runner => runner.RunAsync(null!, null, null, CancellationToken.None),
                cronExpression: "0 2 25 * *",
                options: new RecurringJobOptions
                {
                    QueueName = "critical",
                    TimeZone = TimeZoneInfo.Utc
                });
        }
    }
}
