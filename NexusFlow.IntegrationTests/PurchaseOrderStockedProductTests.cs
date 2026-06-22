using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.AppCore.DTOs.Sales;
using NexusFlow.AppCore.Features.Inventory.Queries;
using NexusFlow.AppCore.Features.Purchasing.Commands;
using NexusFlow.AppCore.Features.Purchasing.PurchaseOrders.Commands;
using NexusFlow.AppCore.Features.Sales.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;

namespace NexusFlow.IntegrationTests
{
    public class PurchaseOrderStockedProductTests : TestBase
    {
        [Fact]
        public async Task RawMaterialPo_ThenGrn_ShouldIncreaseStock()
        {
            var poResult = await SendAsync(CreatePoCommand(productVariantId: 100, quantity: 40, unitCost: 12));
            poResult.Succeeded.Should().BeTrue(poResult.Message);

            var grnResult = await SendAsync(new CreateGrnCommand
            {
                PurchaseOrderId = poResult.Data,
                WarehouseId = 1,
                DateReceived = new DateTime(2026, 6, 17),
                SupplierInvoiceNo = "SUP-RM-001",
                Items = new List<GrnItemDto>
                {
                    new() { ProductVariantId = 100, QuantityReceived = 40, UnitCost = 12 }
                }
            });

            grnResult.Succeeded.Should().BeTrue(grnResult.Message);

            var stock = await SendAsync(new GetAvailableStockQuery { ProductVariantId = 100, WarehouseId = 1 });
            stock.Data.Should().Be(40);
        }

        [Fact]
        public async Task FinishedGoodPo_ThenGrn_ShouldAllowSalesInvoiceToConsumeStock()
        {
            var poResult = await SendAsync(CreatePoCommand(productVariantId: 200, quantity: 15, unitCost: 30));
            poResult.Succeeded.Should().BeTrue(poResult.Message);

            var grnResult = await SendAsync(new CreateGrnCommand
            {
                PurchaseOrderId = poResult.Data,
                WarehouseId = 1,
                DateReceived = new DateTime(2026, 6, 17),
                SupplierInvoiceNo = "SUP-FG-001",
                Items = new List<GrnItemDto>
                {
                    new() { ProductVariantId = 200, QuantityReceived = 15, UnitCost = 30 }
                }
            });

            grnResult.Succeeded.Should().BeTrue(grnResult.Message);

            var beforeStock = await SendAsync(new GetAvailableStockQuery { ProductVariantId = 200, WarehouseId = 1 });
            beforeStock.Data.Should().Be(15);

            var invoiceResult = await SendAsync(new CreateInvoiceCommand
            {
                Invoice = new CreateInvoiceRequest
                {
                    CustomerId = 1,
                    Date = new DateTime(2026, 6, 17),
                    DueDate = new DateTime(2026, 6, 17),
                    WarehouseId = 1,
                    ApplyVat = false,
                    Items = new List<InvoiceLineDto>
                    {
                        new() { ProductVariantId = 200, Quantity = 5, UnitPrice = 100, Discount = 0 }
                    }
                }
            });

            invoiceResult.Succeeded.Should().BeTrue(invoiceResult.Message);

            var afterStock = await SendAsync(new GetAvailableStockQuery { ProductVariantId = 200, WarehouseId = 1 });
            afterStock.Data.Should().Be(10);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
            var salesJournal = await context.JournalEntries
                .Include(j => j.Lines)
                .SingleAsync(j => j.Module == "Sales");

            salesJournal.Lines.Single(l => l.AccountId == 5050).Debit.Should().Be(150);
            salesJournal.Lines.Single(l => l.AccountId == 1032).Credit.Should().Be(150);
        }

        [Fact]
        public async Task CreatePo_ShouldRejectServiceItems()
        {
            await AddServiceVariantAsync();

            var result = await SendAsync(CreatePoCommand(productVariantId: 300, quantity: 1, unitCost: 100));

            result.Succeeded.Should().BeFalse();
            result.Message.Should().Contain("Service item");
        }

        [Fact]
        public async Task CreatePo_ShouldRejectInvalidLinesAndMissingPostingAccountOnApproval()
        {
            var zeroQty = await SendAsync(CreatePoCommand(productVariantId: 100, quantity: 0, unitCost: 10));
            zeroQty.Succeeded.Should().BeFalse();
            zeroQty.Message.Should().Contain("quantities");

            var zeroCost = await SendAsync(CreatePoCommand(productVariantId: 100, quantity: 1, unitCost: 0));
            zeroCost.Succeeded.Should().BeFalse();
            zeroCost.Message.Should().Contain("unit cost");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
                var category = await context.Categories.SingleAsync(x => x.Code == "FG");
                category.InventoryAccountId = null;
                category.CogsAccountId = null;
                await context.SaveChangesAsync(CancellationToken.None);
            }

            var draftResult = await SendAsync(CreatePoCommand(productVariantId: 200, quantity: 1, unitCost: 10, isDraft: true));
            draftResult.Succeeded.Should().BeTrue(draftResult.Message);

            var approvedResult = await SendAsync(CreatePoCommand(productVariantId: 200, quantity: 1, unitCost: 10, isDraft: false));
            approvedResult.Succeeded.Should().BeFalse();
            approvedResult.Message.Should().Contain("Inventory or Expense Account");
        }

        private static CreatePurchaseOrderCommand CreatePoCommand(int productVariantId, decimal quantity, decimal unitCost, bool isDraft = false)
        {
            return new CreatePurchaseOrderCommand
            {
                SupplierId = 1,
                Date = new DateTime(2026, 6, 17),
                ExpectedDate = new DateTime(2026, 6, 18),
                IsDraft = isDraft,
                Note = "Stocked product purchase",
                Items = new List<PurchaseOrderItemDto>
                {
                    new() { ProductVariantId = productVariantId, QuantityOrdered = quantity, UnitCost = unitCost }
                }
            };
        }

        private async Task AddServiceVariantAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
            var brand = await context.Brands.FirstAsync();
            var unit = await context.UnitOfMeasures.FirstAsync();
            var category = await context.Categories.SingleAsync(x => x.Code == "FG");

            var serviceProduct = new Product
            {
                Name = "Delivery Service",
                Brand = brand,
                UnitOfMeasure = unit,
                Category = category,
                Type = ProductType.Service
            };

            serviceProduct.Variants.Add(new ProductVariant
            {
                Id = 300,
                Name = "Delivery Service",
                SKU = "SRV-001",
                IsActive = true,
                CostPrice = 100,
                SellingPrice = 150
            });

            context.Products.Add(serviceProduct);
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }
}
