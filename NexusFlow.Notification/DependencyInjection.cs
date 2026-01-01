using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Notification
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddNotifications(this IServiceCollection services)
        {
            // Register Notification Services
            // services.AddTransient<IEmailService, EmailService>();
            return services;
        }
    }
}
