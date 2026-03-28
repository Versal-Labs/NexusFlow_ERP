using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NexusFlow.AppCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Services
{
    public class FinancialAccountResolver : IFinancialAccountResolver
    {
        private readonly IErpDbContext _context;
        private readonly IMemoryCache _cache;

        public FinancialAccountResolver(IErpDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<int> ResolveAccountIdAsync(string systemConfigKey, CancellationToken cancellationToken = default)
        {
            // 1. Check the ultra-fast Memory Cache first
            string cacheKey = $"AccountResolution_{systemConfigKey}";

            if (_cache.TryGetValue(cacheKey, out int cachedAccountId))
            {
                return cachedAccountId;
            }

            // 2. Cache Miss: Query the Database (Happens only once per config change)
            var config = await _context.SystemConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Key == systemConfigKey, cancellationToken);

            if (config == null || string.IsNullOrWhiteSpace(config.Value))
                throw new Exception($"CRITICAL: Missing System Configuration for '{systemConfigKey}'.");

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Code == config.Value, cancellationToken);

            if (account == null)
                throw new Exception($"CRITICAL: Configured Account Code '{config.Value}' for '{systemConfigKey}' does not exist in the Chart of Accounts.");

            // 3. Store in cache for 12 hours (or until app restart)
            _cache.Set(cacheKey, account.Id, TimeSpan.FromHours(12));

            return account.Id;
        }
    }
}
