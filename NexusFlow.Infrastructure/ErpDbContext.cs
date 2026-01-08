using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Infrastructure.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure
{
    public class ErpDbContext : IdentityDbContext<ApplicationUser>, IErpDbContext
    {
        public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options)
        {
        }

        // Define your DbSets here later, e.g.:
        // public DbSet<StockLayer> StockLayers { get; set; }

        public DbSet<SystemConfig> SystemConfigs { get; set; }
        public DbSet<NumberSequence> NumberSequences { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<TaxType> TaxTypes { get; set; }
        public DbSet<TaxRate> TaxRates { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<UnitOfMeasure> UnitOfMeasures { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<BillOfMaterial> BillOfMaterials { get; set; }
        public DbSet<BomComponent> BomComponents { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // This applies configurations from separate files (Clean Architecture best practice)
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);

            modelBuilder.Entity<ApplicationUser>(b => b.ToTable("Users", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>(b => b.ToTable("Roles", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>(b => b.ToTable("UserRoles", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>(b => b.ToTable("UserClaims", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>(b => b.ToTable("UserLogins", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>(b => b.ToTable("RoleClaims", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>(b => b.ToTable("UserTokens", "Identity"));
        }
    }
}
