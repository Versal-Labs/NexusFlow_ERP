using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Infrastructure.Identity;
using NexusFlow.Infrastructure.Persistence;
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
        public DbSet<StockLayer> StockLayers { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalLine> JournalLines { get; set; }
        public DbSet<FinancialPeriod> FinancialPeriods { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<SalesInvoice> SalesInvoices { get; set; }
        public DbSet<SalesInvoiceItem> SalesInvoiceItems { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<GRN> GRNs { get; set; }
        public DbSet<GRNItem> GRNItems { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // This applies configurations from separate files (Clean Architecture best practice)
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.GetProperties()
                    .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));

                foreach (var property in properties)
                {
                    // We use 18,2 for money. 
                    // If the property name contains 'Qty' or 'Quantity' or 'Rate', 
                    // we use 18,4 for higher precision.
                    if (property.Name.Contains("Qty") ||
                        property.Name.Contains("Quantity") ||   
                        property.Name.Contains("Rate") ||
                        property.Name.Contains("Level"))
                    {
                        property.SetColumnType("decimal(18,4)");
                    }
                    else
                    {
                        property.SetColumnType("decimal(18,2)");
                    }
                }
            }

            modelBuilder.Entity<ApplicationUser>(b => b.ToTable("Users", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>(b => b.ToTable("Roles", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>(b => b.ToTable("UserRoles", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>(b => b.ToTable("UserClaims", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>(b => b.ToTable("UserLogins", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>(b => b.ToTable("RoleClaims", "Identity"));
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>(b => b.ToTable("UserTokens", "Identity"));
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // 1. Handle "AuditableEntity" (CreatedBy, LastModifiedBy)
            // You ideally inject ICurrentUserService to get the real user. 
            // For now, we hardcode "System" or check if you have the service.
            string currentUser = "Admin"; // Replace with _currentUserService.UserId later

            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        entry.Entity.CreatedBy = currentUser;
                        break;
                    case EntityState.Modified:
                        entry.Entity.LastModifiedAt = DateTime.UtcNow;
                        entry.Entity.LastModifiedBy = currentUser;
                        break;
                }
            }

            // 2. Handle "AuditLog" (The JSON Trail)
            var auditEntries = OnBeforeSaveChanges(currentUser);

            var result = await base.SaveChangesAsync(cancellationToken);

            // 3. Save the Audit Logs (We need the IDs generated in Step 2 for 'Added' entries)
            await OnAfterSaveChanges(auditEntries);

            return result;
        }

        // --- HELPER METHODS FOR AUDITING ---

        private List<AuditEntry> OnBeforeSaveChanges(string userId)
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry) { TableName = entry.Entity.GetType().Name, UserId = userId };
                auditEntries.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        // Values generated by DB (like ID on Insert)
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.AuditType = "Create";
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;

                        case EntityState.Deleted:
                            auditEntry.AuditType = "Delete";
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.AuditType = "Update";
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                                auditEntry.ChangedColumns.Add(propertyName);
                            }
                            break;
                    }
                }
            }
            return auditEntries;
        }

        private async Task OnAfterSaveChanges(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0) return;

            foreach (var auditEntry in auditEntries)
            {
                // For new entries, get the ID generated by the DB
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }

                AuditLogs.Add(auditEntry.ToAuditLog());
            }

            await base.SaveChangesAsync(); // Save the logs themselves
        }
    }
}
