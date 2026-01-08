using Microsoft.EntityFrameworkCore;
using NexusFlow.Domain.Entities.Config;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Master;

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

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}