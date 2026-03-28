using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Infrastructure.Identity;
using NexusFlow.Infrastructure.Persistence;
using NexusFlow.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. SQL Server Connection
            services.AddDbContext<ErpDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)));

            services.AddScoped<IErpDbContext>(provider => provider.GetRequiredService<ErpDbContext>());

            services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ErpDbContext>()
            .AddDefaultTokenProviders();


            services.AddScoped<ApplicationDbContextInitialiser>();
            services.AddTransient<ITokenService, JwtTokenService>();
            services.AddScoped<ITaxService, TaxService>();
            services.AddScoped<IStockService, StockService>();
            services.AddScoped<IJournalService, JournalService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<INumberSequenceService, NumberSequenceService>();
            services.AddScoped<IFinancialAccountResolver, FinancialAccountResolver>();

            services.AddSingleton<IUserIdProvider, NameUserIdProvider>();

            return services;
        }
    }
}
