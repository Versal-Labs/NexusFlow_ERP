using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Jobs;
using NexusFlow.AppCore.Jobs.Interfaces;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Infrastructure.Identity;
using NexusFlow.Infrastructure.Jobs.Runners;
using NexusFlow.Infrastructure.Persistence;
using NexusFlow.Infrastructure.Services;
using NexusFlow.Infrastructure.Services.Storage;
using NexusFlow.Infrastructure.Installation;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInstallationInfrastructure(
            this IServiceCollection services,
            InstallationPaths paths,
            IInstallationStateStore stateStore,
            IInstallationSecretStore secretStore,
            InstallationRuntimeOptions? runtimeOptions = null)
        {
            runtimeOptions ??= InstallationRuntimeOptionsFactory.Create(
                new ConfigurationBuilder().AddEnvironmentVariables().Build(),
                paths);
            services.AddSingleton(paths);
            services.AddSingleton(runtimeOptions);
            services.AddSingleton(stateStore);
            services.AddSingleton(secretStore);
            services.AddSingleton<IInstallationSecretStoreDiagnostics>(serviceProvider =>
                secretStore as IInstallationSecretStoreDiagnostics
                ?? throw new InvalidOperationException("The active installation secret store does not support diagnostics."));
            services.AddSingleton<IInstallationRuntimeContext, InstallationRuntimeContext>();
            services.AddSingleton<IInstallationConnectionStringProvider, InstallationConnectionStringProvider>();
            services.AddSingleton<IInstallationDatabaseProvisioner, InstallationDatabaseProvisioner>();
            services.AddScoped<IInstallationTemplateProvider, StandardInstallationTemplateProvider>();
            services.AddScoped<IInstallationReadinessChecker, InstallationReadinessChecker>();
            services.AddScoped<IInstallationOrchestrator, InstallationOrchestrator>();
            return services;
        }

        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. SQL Server Connection
            services.AddDbContext<ErpDbContext>((serviceProvider, options) =>
                options.UseSqlServer(
                    serviceProvider.GetRequiredService<IInstallationConnectionStringProvider>().GetRequiredConnectionString(),
                    b => b.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)));

            services.AddScoped<IErpDbContext>(provider => provider.GetRequiredService<ErpDbContext>());

            services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ErpDbContext>()
            .AddDefaultTokenProviders();

            // Inside AddInfrastructure method
            services.AddSingleton<AzureBlobStorageProvider>(serviceProvider =>
                new AzureBlobStorageProvider(
                    serviceProvider.GetRequiredService<IInstallationSecretStore>()
                        .Get(InstallationConnectionStringProvider.AzureBlobStorageSecret)
                    ?? serviceProvider.GetRequiredService<InstallationRuntimeOptions>().AzureBlobStorageConnectionString,
                    serviceProvider.GetRequiredService<InstallationRuntimeOptions>().AzureBlobStorageContainer));
            services.AddSingleton<LocalDiskStorageProvider>(serviceProvider =>
                new LocalDiskStorageProvider(serviceProvider.GetRequiredService<InstallationPaths>().StoragePath));

            services.AddScoped<IGlobalStorageCoordinator, GlobalStorageCoordinator>();

            services.AddMemoryCache();
            services.AddTransient<ITokenService, JwtTokenService>();
            services.AddScoped<IConfigService, ConfigService>();
            services.AddScoped<ICompanyProfileService, CompanyProfileService>();
            services.AddScoped<IDocumentRenderingService, DocumentRenderingService>();
            services.AddScoped<ITaxService, TaxService>();
            services.AddScoped<IStockService, StockService>();
            services.AddScoped<IJournalService, JournalService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<INumberSequenceService, NumberSequenceService>();
            services.AddScoped<IFinancialAccountResolver, FinancialAccountResolver>();
            services.AddScoped<IExportService, SyncfusionExportService>();
            services.AddScoped<ISmsGatewayService, SmsGatewayService>();
            services.AddScoped<ISecretValidationService, SecretValidationService>();
            services.AddScoped<ICurrentUserPasswordValidator, CurrentUserPasswordValidator>();

            services.AddSingleton<IUserIdProvider, NameUserIdProvider>();

            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();

            services.AddScoped<IProcessDailyAttendanceJob, ProcessDailyAttendanceJob>();

            // Hangfire Runners (Infrastructure wrappers)
            services.AddScoped<ProcessDailyAttendanceJobRunner>();

            // Infrastructure/DependencyInjection.cs
            services.AddScoped<IGenerateDraftPayrollJob, GenerateDraftPayrollJob>();
            services.AddScoped<GenerateDraftPayrollJobRunner>();

            return services;
        }
    }
}
