using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure
{
    public class ErpDbContext : DbContext
    {
        public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options)
        {
        }

        // Define your DbSets here later, e.g.:
        // public DbSet<StockLayer> StockLayers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // This applies configurations from separate files (Clean Architecture best practice)
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);
        }
    }
}
