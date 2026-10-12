using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Sales;

namespace NexusFlow.Infrastructure.Persistence.Configurations;

public sealed class CustomerMigrationBatchConfiguration : IEntityTypeConfiguration<CustomerMigrationBatch>
{
    public void Configure(EntityTypeBuilder<CustomerMigrationBatch> b)
    {
        b.HasIndex(x => x.SourceHash).IsUnique().HasFilter("[Status] <> 'Reversed'");
        b.Property(x => x.Status).IsConcurrencyToken();
    }
}
public sealed class CustomerOpeningItemConfiguration : IEntityTypeConfiguration<CustomerOpeningItem>
{
    public void Configure(EntityTypeBuilder<CustomerOpeningItem> b)
    {
        b.HasIndex(x => new { x.MigrationBatchId, x.SourceRow }).IsUnique();
        b.HasIndex(x => new { x.LegacyCustomerCode, x.ReferenceNo });
        b.HasOne(x => x.MigrationBatch).WithMany().HasForeignKey(x => x.MigrationBatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CustomerCreditApplicationConfiguration : IEntityTypeConfiguration<CustomerCreditApplication>
{
    public void Configure(EntityTypeBuilder<CustomerCreditApplication> b)
    {
        b.ToTable("CustomerCreditApplications", "Finance", t => t.HasCheckConstraint("CK_CustomerCreditApplication_Target", "(CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END) = 1 AND Amount > 0"));
        b.HasOne(x => x.CreditOpeningItem).WithMany().HasForeignKey(x => x.CreditOpeningItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CustomerOpeningItem).WithMany().HasForeignKey(x => x.CustomerOpeningItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SalesInvoice).WithMany().HasForeignKey(x => x.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CustomerDebitMemo).WithMany().HasForeignKey(x => x.CustomerDebitMemoId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class MigrationPaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> b)
    {
        b.ToTable("PaymentAllocations", "Finance", t => t.HasCheckConstraint("CK_PaymentAllocation_Target", "(CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierBillId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierOpeningItemId IS NULL THEN 0 ELSE 1 END) = 1 AND AmountAllocated > 0"));
        b.HasOne(x => x.SupplierOpeningItem).WithMany().HasForeignKey(x => x.SupplierOpeningItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CustomerOpeningItem).WithMany().HasForeignKey(x => x.CustomerOpeningItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CustomerDebitMemo).WithMany().HasForeignKey(x => x.CustomerDebitMemoId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class OpeningChequeConfiguration : IEntityTypeConfiguration<ChequeRegister>
{
    public void Configure(EntityTypeBuilder<ChequeRegister> b)
    {
        b.Property(x => x.LegacyReceiptReference).HasMaxLength(100);
        b.HasIndex(x => new { x.MigrationBatchId, x.LegacyReceiptReference }).HasFilter("[MigrationBatchId] IS NOT NULL");
        b.HasOne(x => x.MigrationBatch).WithMany().HasForeignKey(x => x.MigrationBatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OriginalReceipt).WithMany().HasForeignKey(x => x.OriginalReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("ChequeRegister", "Finance", t => t.HasCheckConstraint("CK_Cheque_Origin", "([MigrationBatchId] IS NULL AND [OriginalReceiptId] IS NOT NULL) OR ([MigrationBatchId] IS NOT NULL AND [OriginalReceiptId] IS NULL AND [OpeningReceivableAccountId] IS NOT NULL AND [OpeningUndepositedAccountId] IS NOT NULL)"));
    }
}
