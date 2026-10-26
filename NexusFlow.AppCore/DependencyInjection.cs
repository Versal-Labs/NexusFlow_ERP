using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FluentValidation;
using MediatR;
using NexusFlow.AppCore.Behaviors;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Services;

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
            services.AddScoped<IFinancialPeriodService, FinancialPeriodService>();
            services.AddScoped<NexusFlow.AppCore.Features.Migration.MigrationCentreService>();
            services.AddScoped<NexusFlow.AppCore.Features.Migration.MigrationExecutionJob>();
            services.AddScoped<NexusFlow.AppCore.Features.Migration.MigrationOperationJob>();
            services.AddScoped<NexusFlow.AppCore.Features.Finance.CustomerMigration.CustomerMigrationService>();
            services.AddScoped<NexusFlow.AppCore.Features.Finance.CustomerMigration.CustomerMasterImportService>();
            services.AddScoped<NexusFlow.AppCore.Services.CustomerBalanceService>();
            services.AddScoped<NexusFlow.AppCore.Services.SupplierBalanceService>();
            services.AddScoped<NexusFlow.AppCore.Services.OpeningBankItemService>();
            services.AddScoped<NexusFlow.AppCore.Services.CustomerSettlementService>();
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FinancialPeriodBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(MigrationRetirementBehavior<,>));

            return services;
        }
    }
}
