using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NexusFlow.Infrastructure.Services
{
    public class CompanyProfileService : ICompanyProfileService
    {
        private readonly IErpDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "CompanyProfile_CacheKey";

        public CompanyProfileService(IErpDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        public async Task<CompanyProfile> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out CompanyProfile? cachedProfile) && cachedProfile != null)
            {
                return cachedProfile;
            }

            var profile = await _dbContext.CompanyProfiles.FirstOrDefaultAsync(cancellationToken);
            
            if (profile == null)
            {
                // Fallback if not initialized (though installation step should guarantee it)
                profile = new CompanyProfile { CompanyName = "Default Company" };
            }

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            };

            _cache.Set(CacheKey, profile, cacheOptions);

            return profile;
        }

        public Task ClearCacheAsync(CancellationToken cancellationToken = default)
        {
            _cache.Remove(CacheKey);
            return Task.CompletedTask;
        }
    }
}
