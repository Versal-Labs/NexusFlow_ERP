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
using NexusFlow.Domain.Entities.Payroll;
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
        public DbSet<MigrationProject> MigrationProjects { get; set; }
        public DbSet<MigrationDataset> MigrationDatasets { get; set; }
        public DbSet<MigrationScope> MigrationScopes { get; set; }
        public DbSet<MigrationUnit> MigrationUnits { get; set; }
        public DbSet<MigrationOrigin> MigrationOrigins { get; set; }
        public DbSet<MigrationRun> MigrationRuns { get; set; }
        public DbSet<MigrationAudit> MigrationAudits { get; set; }
        public DbSet<MigrationOperation> MigrationOperations { get; set; }
        public DbSet<MigrationOperationPayload> MigrationOperationPayloads { get; set; }
        public DbSet<MigrationOperationItem> MigrationOperationItems { get; set; }
        public DbSet<MigrationOperationItemPayload> MigrationOperationItemPayloads { get; set; }
        public DbSet<MigrationOperationIssue> MigrationOperationIssues { get; set; }
        public DbSet<MigrationRunData> MigrationRunPayloads { get; set; }
        private string? migrationActor;
        public IDisposable EnterMigrationActor(string userId)
        {
            var previous = migrationActor; migrationActor = userId;
            return new MigrationWriteLease(() => migrationActor = previous);
        }
        // A session lock survives individual scope transactions. Resume must acquire it too,
        // so even a Deleted/expired Hangfire record cannot race a still-executing worker.
        public async Task<IDisposable> AcquireMigrationWorkerAsync(int projectId, CancellationToken ct = default)
        {
            if (!Database.IsSqlServer()) return new MigrationWriteLease(() => { });
            var connection = new Microsoft.Data.SqlClient.SqlConnection(Database.GetConnectionString());
            try
            {
                await connection.OpenAsync(ct);
                using var command = connection.CreateCommand();
                command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @r;";
                command.Parameters.AddWithValue("@resource", "NexusFlow.Migration.Worker." + projectId);
                if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) < 0) throw new InvalidOperationException("A migration worker is still active. Check progress before resuming.");
                return new MigrationWriteLease(() =>
                {
                    // Do not return a session holding an application lock to the pool.
                    try { using var release = connection.CreateCommand(); release.CommandText = "EXEC sys.sp_releaseapplock @Resource=@resource, @LockOwner='Session';"; release.Parameters.AddWithValue("@resource", "NexusFlow.Migration.Worker." + projectId); release.ExecuteNonQuery(); }
                    finally { connection.Dispose(); }
                });
            }
            catch { connection.Dispose(); throw; }
        }
        public DbSet<SupplierOpeningItem> SupplierOpeningItems { get; set; }
        public DbSet<SupplierCreditApplication> SupplierCreditApplications { get; set; }
        public DbSet<OpeningBankItem> OpeningBankItems { get; set; }
        private int? migrationPostingProject;
        public IDisposable EnterMigrationPosting(int projectId)
        {
            if (migrationPostingProject != null) throw new InvalidOperationException("Nested migration posting is not permitted.");
            migrationPostingProject = projectId; return new MigrationWriteLease(() => migrationPostingProject = null);
        }
        private sealed class MigrationWriteLease(Action dispose) : IDisposable
        {
            private Action? release = dispose;
            public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
        }
        public async Task LockMigrationProjectAsync(int projectId, CancellationToken ct = default)
        {
            if (!Database.IsSqlServer()) return;
            var resource = "NexusFlow.Migration." + projectId;
            await Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=30000; IF @result < 0 THROW 51000, 'Migration project is busy. Refresh and retry.', 1;", ct);
        }
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
        public DbSet<BarcodeTemplate> BarcodeTemplates { get; set; }
        public DbSet<BillOfMaterial> BillOfMaterials { get; set; }
        public DbSet<BomComponent> BomComponents { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<StockLayer> StockLayers { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }
        public DbSet<ProductionOrder> ProductionOrders { get; set; }
        public DbSet<ProductionOrderComponent> ProductionOrderComponents { get; set; }
        public DbSet<ProductionOrderRevision> ProductionOrderRevisions { get; set; }
        public DbSet<ProductionMaterialMovement> ProductionMaterialMovements { get; set; }
        public DbSet<ProductionMaterialMovementLine> ProductionMaterialMovementLines { get; set; }
        public DbSet<ProductionReceipt> ProductionReceipts { get; set; }
        public DbSet<ProductionReceiptConsumption> ProductionReceiptConsumptions { get; set; }
        public DbSet<ProductionSupplierClaim> ProductionSupplierClaims { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalLine> JournalLines { get; set; }
        public DbSet<FinancialPeriod> FinancialPeriods { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerMigrationBatch> CustomerMigrationBatches { get; set; }
        public DbSet<CustomerOpeningItem> CustomerOpeningItems { get; set; }
        public DbSet<CustomerCreditApplication> CustomerCreditApplications { get; set; }
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
        public DbSet<CustomerDebitMemo> CustomerDebitMemos { get; set; }
        public DbSet<StockTake> StockTakes { get; set; }
        public DbSet<StockTakeItem> StockTakeItems { get; set; }
        public DbSet<ChequeRegister> ChequeRegisters { get; set; }
        public DbSet<Bank> Banks { get; set; }
        public DbSet<BankBranch> BankBranches { get; set; }
        public DbSet<BankReconciliation> BankReconciliations { get; set; }
        public DbSet<Province> Provinces { get; set; }
        public DbSet<District> Districts { get; set; }
        public DbSet<City> Cities { get; set; }

        public DbSet<PayrollComponent> PayrollComponents { get; set; }
        public DbSet<EmployeePayrollComponent> EmployeePayrollComponents { get; set; }
        public DbSet<SalaryAdvance> SalaryAdvances { get; set; }
        public DbSet<EmployeeLoan> EmployeeLoans { get; set; }
        public DbSet<LoanRepaymentSchedule> LoanRepaymentSchedules { get; set; }
        public DbSet<ShiftProfile> ShiftProfiles { get; set; }
        public DbSet<AttendanceLog> AttendanceLogs { get; set; }
        public DbSet<DailyAttendanceRecord> DailyAttendanceRecords { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<PayrollPeriod> PayrollPeriods { get; set; }
        public DbSet<PayrollSlip> PayrollSlips { get; set; }
        public DbSet<PayrollSlipLineItem> PayrollSlipLineItems { get; set; }
        public DbSet<InstallationRecord> InstallationRecords { get; set; }
        public DbSet<AppliedInstallationStep> AppliedInstallationSteps { get; set; }
        public DbSet<CompanyProfile> CompanyProfiles { get; set; }
        public DbSet<DocumentTemplate> DocumentTemplates { get; set; }
        public DbSet<GeneratedDocument> GeneratedDocuments { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // This applies configurations from separate files (Clean Architecture best practice)
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);

            // Production is retained as an audit chain; restrictive deletes prevent an order deletion from erasing costing evidence.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                .Where(x => x.ClrType.Namespace == "NexusFlow.Domain.Entities.Inventory" && x.ClrType.Name.StartsWith("Production", StringComparison.Ordinal)))
            {
                foreach (var foreignKey in entityType.GetForeignKeys()) foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.GetProperties()
                    .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));

                foreach (var property in properties)
                {
                    if (entityType.ClrType == typeof(BarcodeTemplate))
                    {
                        property.SetColumnType("decimal(9,3)");
                        continue;
                    }

                    // We use 18,2 for money. 
                    // If the property name contains 'Qty' or 'Quantity' or 'Rate', 
                    // we use 18,4 for higher precision.
                    if (property.Name.Contains("UnitCost") || property.Name is "CostPrice" or "MovingAverageCost")
                    {
                        // Retain the sixteen integer digits of the former decimal(18,2) costs.
                        property.SetColumnType("decimal(22,6)");
                    }
                    else if (property.Name.Contains("Qty") ||
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
                        entityType.ClrType == typeof(NotificationItem) ||
                        entityType.ClrType == typeof(MigrationOperation) ||
                        entityType.ClrType == typeof(MigrationOperationPayload) ||
                        entityType.ClrType == typeof(MigrationOperationItem) ||
                        entityType.ClrType == typeof(MigrationOperationItemPayload) ||
                        entityType.ClrType == typeof(MigrationOperationIssue) ||
                        entityType.ClrType == typeof(MigrationRunData))
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
            if (migrationPostingProject == null)
                await NexusFlow.AppCore.Features.Migration.MigrationTradingGuard.CheckAsync(this, ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified).Select(x => x.Entity).ToList(), cancellationToken);
            string currentUser = migrationActor ?? _currentUserService.UserId;

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

        public async Task<IDbContextTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        {
            // InMemory is used by non-relational tests; SQL Server honors the requested isolation level.
            return Database.IsRelational()
                ? await Database.BeginTransactionAsync(isolationLevel, cancellationToken)
                : await Database.BeginTransactionAsync(cancellationToken);
        }
    }
}
