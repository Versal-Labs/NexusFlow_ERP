using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NexusFlow.AppCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Services
{
    internal class ConfigService : IConfigService
    {
        private readonly IErpDbContext _context;
        private readonly IMemoryCache _cache;

        public ConfigService(IErpDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<decimal> GetDecimal(string key)
        {
            // Try to get from Cache (RAM) first
            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // Refresh every 10 mins

                var config = await _context.SystemConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Key == key);

                // Default to 0 if not found to prevent crashes
                return config != null ? decimal.Parse(config.Value) : 0m;
            });
        }

        public async Task<bool> GetBool(string key)
        {
            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                var config = await _context.SystemConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Key == key);

                // 1. Handle if config or Value is null
                if (config?.Value == null)
                {
                    return false; // Return a boolean, not 0m
                }

                // 2. Safely try to parse the string to a bool
                // This handles "True"/"False" safely without crashing
                if (bool.TryParse(config.Value, out bool result))
                {
                    return result;
                }

                // 3. (Optional) Handle "1" or "0" if your DB uses bits/integers
                if (config.Value == "1") return true;
                if (config.Value == "0") return false;

                // Default to false if parsing fails completely
                return false;
            });
        }

        public async Task<string> GetString(string key)
        {
            // Note: Added 'async' to the method signature
            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                var config = await _context.SystemConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Key == key);

                // usage of the Null Coalescing Operator (??)
                // If config is null OR config.Value is null, return string.Empty
                return config?.Value ?? string.Empty;
            });
        }
    }
}
