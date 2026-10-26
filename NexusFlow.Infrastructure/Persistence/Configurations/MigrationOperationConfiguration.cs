using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Finance;

namespace NexusFlow.Infrastructure.Persistence.Configurations;

public sealed class MigrationOperationConfiguration : IEntityTypeConfiguration<MigrationOperation>
{
    public void Configure(EntityTypeBuilder<MigrationOperation> b)
    {
        b.HasOne<MigrationProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MigrationRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProjectId, x.ClientRequestId }).IsUnique();
        b.HasIndex(x => x.ProjectId).IsUnique().HasFilter("[Status] IN ('Queued','Running')");
        b.Property(x => x.Status).IsConcurrencyToken();
    }
}
public sealed class MigrationOperationPayloadConfiguration : IEntityTypeConfiguration<MigrationOperationPayload>
{
    public void Configure(EntityTypeBuilder<MigrationOperationPayload> b)
    {
        b.HasOne<MigrationOperation>().WithMany().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.OperationId).IsUnique();
    }
}
public sealed class MigrationOperationItemConfiguration : IEntityTypeConfiguration<MigrationOperationItem>
{
    public void Configure(EntityTypeBuilder<MigrationOperationItem> b)
    {
        b.HasOne<MigrationOperation>().WithMany().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OperationId, x.Ordinal }).IsUnique();
        b.HasIndex(x => new { x.OperationId, x.Status, x.Id });
    }
}
public sealed class MigrationOperationItemPayloadConfiguration : IEntityTypeConfiguration<MigrationOperationItemPayload>
{
    public void Configure(EntityTypeBuilder<MigrationOperationItemPayload> b)
    {
        b.HasOne<MigrationOperationItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ItemId).IsUnique();
    }
}
public sealed class MigrationOperationIssueConfiguration : IEntityTypeConfiguration<MigrationOperationIssue>
{
    public void Configure(EntityTypeBuilder<MigrationOperationIssue> b)
    {
        b.HasOne<MigrationOperation>().WithMany().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MigrationOperationItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OperationId, x.Severity, x.Id });
    }
}
public sealed class MigrationRunDataConfiguration : IEntityTypeConfiguration<MigrationRunData>
{
    public void Configure(EntityTypeBuilder<MigrationRunData> b)
    {
        b.HasOne<MigrationRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.RunId).IsUnique();
    }
}
