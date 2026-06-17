using NexusFlow.Domain.Entities.System;
using System.Threading;
using System.Threading.Tasks;

namespace NexusFlow.AppCore.Interfaces
{
    public interface ICompanyProfileService
    {
        Task<CompanyProfile> GetProfileAsync(CancellationToken cancellationToken = default);
        Task ClearCacheAsync(CancellationToken cancellationToken = default);
    }
}
