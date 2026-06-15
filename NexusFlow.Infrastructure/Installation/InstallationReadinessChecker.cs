using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class InstallationReadinessChecker : IInstallationReadinessChecker
    {
        private readonly ErpDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IInstallationDatabaseProvisioner _databaseProvisioner;

        public InstallationReadinessChecker(
            ErpDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IInstallationDatabaseProvisioner databaseProvisioner)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
            _databaseProvisioner = databaseProvisioner;
        }

        public async Task<ReadinessReport> CheckAsync(CancellationToken cancellationToken = default)
        {
            var checks = new List<ReadinessCheck>();

            checks.Add(await CheckDatabaseAsync(cancellationToken));
            if (!checks[^1].Passed)
            {
                return new ReadinessReport { Checks = checks };
            }

            var pending = await _databaseProvisioner.GetPendingMigrationsAsync(cancellationToken);
            checks.Add(new("database.migrations", "No database migrations are pending", pending.Count == 0,
                pending.Count == 0 ? null : string.Join(", ", pending)));

            var configs = await _context.SystemConfigs.AsNoTracking().ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);
            var missingConfigs = ConfigurationKeys.Required.Where(x => !configs.TryGetValue(x, out var value) || string.IsNullOrWhiteSpace(value)).ToArray();
            checks.Add(new("configuration.required", "All required system settings exist", missingConfigs.Length == 0,
                missingConfigs.Length == 0 ? null : string.Join(", ", missingConfigs)));

            var accountCodes = await _context.Accounts.AsNoTracking()
                .Where(x => x.IsActive && x.IsTransactionAccount)
                .Select(x => x.Code)
                .ToHashSetAsync(cancellationToken);
            var missingMappings = AccountMappingKeys.Required.Where(key =>
                !configs.TryGetValue(key, out var code) || !accountCodes.Contains(code)).ToArray();
            checks.Add(new("configuration.account-mappings", "All required financial mappings resolve to active transaction accounts",
                missingMappings.Length == 0, missingMappings.Length == 0 ? null : string.Join(", ", missingMappings)));

            var sequenceNames = await _context.NumberSequences.AsNoTracking().Select(x => x.Module).ToHashSetAsync(cancellationToken);
            var missingSequences = NumberSequenceKeys.Required.Where(x => !sequenceNames.Contains(x)).ToArray();
            checks.Add(new("configuration.number-sequences", "All required document sequences exist", missingSequences.Length == 0,
                missingSequences.Length == 0 ? null : string.Join(", ", missingSequences)));

            checks.Add(new("operations.warehouse", "At least one active warehouse exists",
                await _context.Warehouses.AnyAsync(x => x.IsActive, cancellationToken)));
            checks.Add(new("operations.financial-period", "At least one open financial period exists",
                await _context.FinancialPeriods.AnyAsync(x => !x.IsClosed, cancellationToken)));

            var superAdminRole = await _roleManager.FindByNameAsync(DefaultRoleManifest.SuperAdmin);
            var superAdminUsers = superAdminRole == null
                ? new List<ApplicationUser>()
                : (await _userManager.GetUsersInRoleAsync(DefaultRoleManifest.SuperAdmin)).Where(x => x.IsActive).ToList();
            checks.Add(new("security.super-admin", "At least one active SuperAdmin exists", superAdminUsers.Count > 0));

            var superAdminClaims = superAdminRole == null
                ? Array.Empty<System.Security.Claims.Claim>()
                : (await _roleManager.GetClaimsAsync(superAdminRole)).ToArray();
            checks.Add(new("security.super-admin-permission", "SuperAdmin has the protected bypass permission",
                superAdminClaims.Any(x => x.Type == "Permission" && x.Value == Permissions.SuperAdmin)));

            var missingRoles = DefaultRoleManifest.Roles.Keys
                .Where(roleName => !_roleManager.Roles.Any(role => role.Name == roleName))
                .ToArray();
            checks.Add(new("security.roles", "Recommended security roles exist", missingRoles.Length == 0,
                missingRoles.Length == 0 ? null : string.Join(", ", missingRoles)));

            var localStorage = configs.GetValueOrDefault(ConfigurationKeys.StorageLocalPath);
            checks.Add(CheckStorage(localStorage));

            checks.Add(new("installation.record", "Installation metadata exists",
                await _context.InstallationRecords.AnyAsync(cancellationToken)));

            return new ReadinessReport { Checks = checks };
        }

        private async Task<ReadinessCheck> CheckDatabaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                return new("database.connection", "Database connection is available",
                    await _context.Database.CanConnectAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                return new("database.connection", "Database connection is available", false, ex.Message);
            }
        }

        private static ReadinessCheck CheckStorage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new("storage.local", "Local storage is writable", false, "Storage.LocalPath is not configured.");
            }

            try
            {
                Directory.CreateDirectory(path);
                var probe = Path.Combine(path, $".nexusflow-write-test-{Guid.NewGuid():N}");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return new("storage.local", "Local storage is writable", true);
            }
            catch (Exception ex)
            {
                return new("storage.local", "Local storage is writable", false, ex.Message);
            }
        }
    }
}
