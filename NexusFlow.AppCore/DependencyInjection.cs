using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FluentValidation;

namespace NexusFlow.AppCore
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddAppCore(this IServiceCollection services)
        {
            var assembly = typeof(AppCoreAssemblyMarker).Assembly;

            services.AddAutoMapper(cfg => { }, assembly);

            services.AddValidatorsFromAssembly(assembly);

            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(assembly)
            );

            return services;
        }
    }
}
