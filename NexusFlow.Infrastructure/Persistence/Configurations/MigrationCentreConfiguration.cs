using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Purchasing;

namespace NexusFlow.Infrastructure.Persistence.Configurations;

public sealed class MigrationProjectConfiguration : IEntityTypeConfiguration<MigrationProject>
{
    public void Configure(EntityTypeBuilder<MigrationProject> b)
    {
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.Status).IsUnique().HasFilter("[Status] <> 'Finalized'");
        b.ToTable("MigrationProjects", "Migration", t => t.HasCheckConstraint("CK_Migration_Dates", "[OpeningDate] = DATEADD(day, 1, [ClosingDate]) AND [Currency] = 'LKR'"));
    }
}
public sealed class MigrationDatasetConfiguration : IEntityTypeConfiguration<MigrationDataset>
{
    public void Configure(EntityTypeBuilder<MigrationDataset> b)
    {
        b.HasOne<MigrationProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProjectId, x.Module, x.Revision }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.Module }).IsUnique().HasFilter("[IsCurrent] = 1");
    }
}
public sealed class MigrationScopeConfiguration : IEntityTypeConfiguration<MigrationScope>
{
    public void Configure(EntityTypeBuilder<MigrationScope> b)
    {
        b.HasOne<MigrationProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProjectId, x.Kind, x.Code }).IsUnique();
    }
}
public sealed class MigrationUnitConfiguration : IEntityTypeConfiguration<MigrationUnit>
{
    public void Configure(EntityTypeBuilder<MigrationUnit> b)
    {
        b.HasOne<MigrationProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProjectId, x.Module, x.ScopeKey }).IsUnique().HasFilter("[Status] = 'Posted'");
        b.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.ReversalJournalEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class MigrationOriginConfiguration : IEntityTypeConfiguration<MigrationOrigin>
{
    public void Configure(EntityTypeBuilder<MigrationOrigin> b)
    {
        b.HasOne<MigrationUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SourceSystem, x.RecordKind, x.IdentityHash }).IsUnique().HasFilter("[IsCurrent] = 1");
    }
}
public sealed class MigrationRunConfiguration : IEntityTypeConfiguration<MigrationRun>
{
    public void Configure(EntityTypeBuilder<MigrationRun> b)
    {
        b.HasOne<MigrationProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ProjectId).IsUnique().HasFilter("[Status] IN ('Queued','Running')");
        b.Property(x => x.Status).IsConcurrencyToken();
    }
}
public sealed class SupplierOpeningConfiguration : IEntityTypeConfiguration<SupplierOpeningItem>
{
    public void Configure(EntityTypeBuilder<SupplierOpeningItem> b)
    {
        b.HasOne<MigrationUnit>().WithMany().HasForeignKey(x => x.MigrationUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.PayableAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SupplierCreditConfiguration : IEntityTypeConfiguration<SupplierCreditApplication>
{
    public void Configure(EntityTypeBuilder<SupplierCreditApplication> b)
    {
        b.HasOne<SupplierOpeningItem>().WithMany().HasForeignKey(x => x.CreditOpeningItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SupplierOpeningItem>().WithMany().HasForeignKey(x => x.SupplierOpeningItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SupplierBill>().WithMany().HasForeignKey(x => x.SupplierBillId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("SupplierCreditApplications", "Finance", t => t.HasCheckConstraint("CK_SupplierCredit_Target", "[Amount] > 0 AND (([SupplierOpeningItemId] IS NULL AND [SupplierBillId] IS NOT NULL) OR ([SupplierOpeningItemId] IS NOT NULL AND [SupplierBillId] IS NULL))"));
    }
}
public sealed class OpeningBankConfiguration : IEntityTypeConfiguration<OpeningBankItem>
{
    public void Configure(EntityTypeBuilder<OpeningBankItem> b)
    {
        b.HasOne<MigrationUnit>().WithMany().HasForeignKey(x => x.MigrationUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReceivedCheque).WithMany().HasForeignKey(x => x.ReceivedChequeId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SupplierLegacyConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b) => b.HasIndex(x => x.LegacySupplierCode).IsUnique().HasFilter("[LegacySupplierCode] IS NOT NULL");
}
public sealed class MigrationAuditConfiguration : IEntityTypeConfiguration<MigrationAudit>
{
    public void Configure(EntityTypeBuilder<MigrationAudit> b) => b.HasOne<MigrationProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
}
public sealed class OpeningEndorsedSupplierConfiguration : IEntityTypeConfiguration<ChequeRegister>
{
    public void Configure(EntityTypeBuilder<ChequeRegister> b) => b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.OpeningEndorsedSupplierId).OnDelete(DeleteBehavior.Restrict);
}
