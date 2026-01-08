using Microsoft.EntityFrameworkCore;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IErpDbContext
    {
        DbSet<Account> Accounts { get; set; }
        DbSet<NumberSequence> NumberSequences { get; set; }
        DbSet<SystemConfig> SystemConfigs { get; set; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}