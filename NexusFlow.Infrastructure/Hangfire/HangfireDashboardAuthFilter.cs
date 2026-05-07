using Hangfire.Dashboard;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Hangfire
{
    public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // Only allow authenticated users who are in the Admin role
            return httpContext.User.Identity?.IsAuthenticated == true
                && httpContext.User.IsInRole("Admin"); // Adjust to your role name
        }
    }
}
