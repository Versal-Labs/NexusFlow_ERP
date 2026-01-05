using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusFlow.Infrastructure.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Persistence
{
    public class ApplicationDbContextInitialiser
    {
        private readonly ILogger<ApplicationDbContextInitialiser> _logger;
        private readonly ErpDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ApplicationDbContextInitialiser(
                            ILogger<ApplicationDbContextInitialiser> logger,
                            ErpDbContext context,
                            UserManager<ApplicationUser> userManager,
                            RoleManager<IdentityRole> roleManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task InitialiseAsync()
        {
            try
            {
                if (_context.Database.IsSqlServer())
                {
                    await _context.Database.MigrateAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing the database.");
                throw;
            }
        }

        public async Task SeedAsync()
        {
            try
            {
                await TrySeedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }

        public async Task TrySeedAsync()
        {
            // 1. Seed Roles
            var roles = new[] { "Admin", "Accountant", "StoreKeeper", "SalesRep" };

            foreach (var roleName in roles)
            {
                if (_roleManager.Roles.All(r => r.Name != roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed Default Admin User
            var administrator = new ApplicationUser
            {
                UserName = "admin@nexusflow.com",
                Email = "admin@nexusflow.com",
                FullName = "System Administrator",
                IsActive = true,
                EmailConfirmed = true
            };

            if (_userManager.Users.All(u => u.UserName != administrator.UserName))
            {
                await _userManager.CreateAsync(administrator, "Admin@123"); // Default Password
                await _userManager.AddToRolesAsync(administrator, new[] { "Admin" });
            }
        }
    }
}
