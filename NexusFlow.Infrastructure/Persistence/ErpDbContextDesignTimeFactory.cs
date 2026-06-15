using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NexusFlow.Infrastructure.Installation;

namespace NexusFlow.Infrastructure.Persistence
{
    public sealed class ErpDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ErpDbContext>
    {
        public ErpDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("NEXUSFLOW_DESIGNTIME_CONNECTION")
                ?? "Server=(localdb)\\MSSQLLocalDB;Database=NexusFlow_DesignTime;Trusted_Connection=True;TrustServerCertificate=True";

            var options = new DbContextOptionsBuilder<ErpDbContext>()
                .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName))
                .Options;

            return new ErpDbContext(options, new SystemCurrentUserService());
        }
    }
}
