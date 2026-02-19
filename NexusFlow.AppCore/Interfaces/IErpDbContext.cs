using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Entities.System;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IErpDbContext
    {
        DbSet<Account> Accounts { get; set; }
        DbSet<NumberSequence> NumberSequences { get; set; }
        DbSet<SystemConfig> SystemConfigs { get; set; }
        DbSet<TaxType> TaxTypes { get; set; }
        DbSet<TaxRate> TaxRates { get; set; }
        DbSet<Brand> Brands { get; set; }
        DbSet<Category> Categories { get; set; }
        DbSet<UnitOfMeasure> UnitOfMeasures { get; set; }
        DbSet<Product> Products { get; set; }
        DbSet<ProductVariant> ProductVariants { get; set; }
        DbSet<BillOfMaterial> BillOfMaterials { get; set; }
        DbSet<BomComponent> BomComponents { get; set; }
        DbSet<Warehouse> Warehouses { get; set; }
        DbSet<StockLayer> StockLayers { get; set; }
        DbSet<StockTransaction> StockTransactions { get; set; }
        DbSet<JournalEntry> JournalEntries { get; set; }
        DbSet<JournalLine> JournalLines { get; set; }
        DbSet<FinancialPeriod> FinancialPeriods { get; set; }
        DbSet<Customer> Customers { get; set; }
        DbSet<SalesInvoice> SalesInvoices { get; set; }
        DbSet<SalesInvoiceItem> SalesInvoiceItems { get; set; }
        DbSet<Supplier> Suppliers { get; set; }
        DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        DbSet<GRN> GRNs { get; set; }
        DbSet<AuditLog> AuditLogs { get; set; }
        DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        DbSet<NotificationItem> Notifications { get; set; }
        DbSet<SystemLookup> SystemLookups { get; set; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);

        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}