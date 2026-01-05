using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.Infrastructure.Identity;
using NexusFlow.Infrastructure.Persistence;
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

            services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ErpDbContext>()
            .AddDefaultTokenProviders();


            services.AddScoped<ApplicationDbContextInitialiser>();

            return services;
        }
    }
}
