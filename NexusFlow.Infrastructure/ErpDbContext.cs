using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.HR;
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
using static Microsoft.Data.SqlClient.Internal.SqlClientEventSource;

namespace NexusFlow.Infrastructure
{
    public class ErpDbContext : IdentityDbContext<ApplicationUser>, IErpDbContext
    {
        private readonly ICurrentUserService _currentUserService;
        public ErpDbContext(DbContextOptions<ErpDbContext> options, ICurrentUserService currentUserService) : base(options)
        {
            _currentUserService = currentUserService;
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
        public DbSet<NotificationItem> Notifications { get; set; }
        public DbSet<SystemLookup> SystemLookups { get; set; }
        public DbSet<PaymentAllocation> PaymentAllocations { get; set; }
        public DbSet<SupplierBill> SupplierBills { get; set; }
        public DbSet<SupplierBillItem> SupplierBillItems { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<SalesOrder> SalesOrders { get; set; }
        public DbSet<SalesOrderItem> SalesOrderItems { get; set; }
        public DbSet<CommissionRule> CommissionRules { get; set; }
        public DbSet<CommissionLedger> CommissionLedgers { get; set; }
        public DbSet<CreditNote> CreditNotes { get; set; }
        public DbSet<CreditNoteItem> CreditNoteItems { get; set; }
        public DbSet<StockTake> StockTakes { get; set; }
        public DbSet<StockTakeItem> StockTakeItems { get; set; }
        public DbSet<ChequeRegister> ChequeRegisters { get; set; }
        public DbSet<Bank> Banks { get; set; }
        public DbSet<BankBranch> BankBranches { get; set; }
        public DbSet<BankReconciliation> BankReconciliations { get; set; }
        public DbSet<Province> Provinces { get; set; }
        public DbSet<District> Districts { get; set; }
        public DbSet<City> Cities { get; set; }

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

            // 3. ARCHITECTURAL MANDATE: SQL Server Temporal Tables
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType) ||
                    typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
                {
                    if (entityType.ClrType == typeof(AuditLog) ||
                        entityType.ClrType == typeof(NotificationItem))
                        continue;

                    string tableName = entityType.GetTableName() ?? entityType.ClrType.Name;
                    string schema = entityType.GetSchema() ?? "dbo";

                    // THE REAL FIX: The EF Core method is IsTemporal()
                    modelBuilder.Entity(entityType.ClrType).ToTable(tableName, schema, tb => tb.IsTemporal(t =>
                    {
                        t.UseHistoryTable($"{tableName}_History", schema);
                        t.HasPeriodStart("ValidFrom");
                        t.HasPeriodEnd("ValidTo");
                    }));
                }
            }
        }


        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            string currentUser = _currentUserService.UserId;

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

            var result = await base.SaveChangesAsync(cancellationToken);

            return result;
        }

        

        

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return await Database.BeginTransactionAsync(cancellationToken);
        }
    }
}
