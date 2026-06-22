using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.DTOs.Sales;
using NexusFlow.AppCore.Features.Inventory.Commands;
using NexusFlow.AppCore.Features.Inventory.Queries;
using NexusFlow.AppCore.Features.Sales.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;

namespace NexusFlow.IntegrationTests
{
    public class OpeningStockTests : TestBase
    {
        [Fact]
        public async Task PostOpeningStock_ForFinishedGood_ShouldCreateLayerTransactionAndBalancedJournal()
        {
            var result = await SendAsync(CreateOpeningStockCommand(sellingPrice: 2400));

            result.Succeeded.Should().BeTrue(result.Message);
            result.Data.Should().StartWith("OBSTK-");

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IErpDbContext>();

            var layer = await context.StockLayers.SingleAsync(x => x.ProductVariantId == 200 && x.WarehouseId == 1);
            layer.InitialQty.Should().Be(25);
            layer.RemainingQty.Should().Be(25);
            layer.UnitCost.Should().Be(1200);
            layer.BatchNo.Should().Be("OB-LOT-001");
            layer.DateReceived.Date.Should().Be(new DateTime(2026, 6, 17));
            layer.IsExhausted.Should().BeFalse();

            var stockTransaction = await context.StockTransactions.SingleAsync(x => x.ReferenceDocNo == result.Data);
            stockTransaction.Type.Should().Be(StockTransactionType.OpeningBalance);
            stockTransaction.Qty.Should().Be(25);
            stockTransaction.TotalValue.Should().Be(30000);

            var variant = await context.ProductVariants.SingleAsync(x => x.Id == 200);
            variant.CostPrice.Should().Be(1200);
            variant.MovingAverageCost.Should().Be(1200);
            variant.SellingPrice.Should().Be(2400);

            var journal = await context.JournalEntries
                .Include(j => j.Lines)
                .SingleAsync(j => j.ReferenceNo == result.Data);

            journal.Module.Should().Be("Inventory");
            journal.Lines.Sum(l => l.Debit).Should().Be(30000);
            journal.Lines.Sum(l => l.Credit).Should().Be(30000);
            journal.Lines.Single(l => l.AccountId == 1032).Debit.Should().Be(30000);
            journal.Lines.Single(l => l.AccountId == 3200).Credit.Should().Be(30000);
        }

        [Fact]
        public async Task CreateInvoice_AfterOpeningStock_ShouldConsumeFifoAndPostCogs()
        {
            var openingResult = await SendAsync(CreateOpeningStockCommand(quantity: 25, unitCost: 20));
            openingResult.Succeeded.Should().BeTrue(openingResult.Message);

            var beforeStock = await SendAsync(new GetAvailableStockQuery { ProductVariantId = 200, WarehouseId = 1 });
            beforeStock.Data.Should().Be(25);

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
                        new() { ProductVariantId = 200, Quantity = 10, UnitPrice = 50, Discount = 0 }
                    }
                }
            });

            invoiceResult.Succeeded.Should().BeTrue(invoiceResult.Message);

            var afterStock = await SendAsync(new GetAvailableStockQuery { ProductVariantId = 200, WarehouseId = 1 });
            afterStock.Data.Should().Be(15);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IErpDbContext>();

            var layer = await context.StockLayers.SingleAsync(x => x.ProductVariantId == 200 && x.WarehouseId == 1);
            layer.RemainingQty.Should().Be(15);

            var salesJournal = await context.JournalEntries
                .Include(j => j.Lines)
                .SingleAsync(j => j.Module == "Sales");

            salesJournal.Lines.Single(l => l.AccountId == 5050).Debit.Should().Be(200);
            salesJournal.Lines.Single(l => l.AccountId == 1032).Credit.Should().Be(200);
        }

        [Fact]
        public async Task PostOpeningStock_ShouldRejectInvalidLinesAndSecondOpeningForSameVariantWarehouse()
        {
            var zeroQuantity = await SendAsync(CreateOpeningStockCommand(quantity: 0));
            zeroQuantity.Succeeded.Should().BeFalse();
            zeroQuantity.Message.Should().Contain("quantity");

            var decimalQuantity = await SendAsync(CreateOpeningStockCommand(quantity: 1.5m));
            decimalQuantity.Succeeded.Should().BeFalse();
            decimalQuantity.Message.Should().Contain("whole number");

            var zeroCost = await SendAsync(CreateOpeningStockCommand(unitCost: 0));
            zeroCost.Succeeded.Should().BeFalse();
            zeroCost.Message.Should().Contain("unit cost");

            var duplicateVariant = await SendAsync(new PostOpeningStockCommand
            {
                OpeningDate = new DateTime(2026, 6, 17),
                WarehouseId = 1,
                Notes = "Duplicate variant test",
                Items = new List<OpeningStockLineRequest>
                {
                    new() { ProductVariantId = 200, Quantity = 1, UnitCost = 10 },
                    new() { ProductVariantId = 200, Quantity = 2, UnitCost = 10 }
                }
            });
            duplicateVariant.Succeeded.Should().BeFalse();
            duplicateVariant.Message.Should().Contain("Duplicate variants");

            var firstOpening = await SendAsync(CreateOpeningStockCommand());
            firstOpening.Succeeded.Should().BeTrue(firstOpening.Message);

            var secondOpening = await SendAsync(CreateOpeningStockCommand(quantity: 1, unitCost: 20));
            secondOpening.Succeeded.Should().BeFalse();
            secondOpening.Message.Should().Contain("stock activity has started");
        }

        [Fact]
        public async Task PostOpeningStock_ShouldRejectServiceItemsAndMissingGlConfiguration()
        {
            await AddServiceVariantAsync();

            var serviceResult = await SendAsync(new PostOpeningStockCommand
            {
                OpeningDate = new DateTime(2026, 6, 17),
                WarehouseId = 1,
                Notes = "Service validation",
                Items = new List<OpeningStockLineRequest>
                {
                    new() { ProductVariantId = 300, Quantity = 1, UnitCost = 100 }
                }
            });

            serviceResult.Succeeded.Should().BeFalse();
            serviceResult.Message.Should().Contain("Service item");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
                var config = await context.SystemConfigs.SingleAsync(x => x.Key == "Account.Equity.OpeningBalance");
                context.SystemConfigs.Remove(config);
                await context.SaveChangesAsync(CancellationToken.None);
            }

            var missingEquityResult = await SendAsync(CreateOpeningStockCommand());
            missingEquityResult.Succeeded.Should().BeFalse();
            missingEquityResult.Message.Should().Contain("Opening Balance Equity");
        }

        [Fact]
        public async Task PostOpeningStock_ShouldRejectMissingInventoryAccountOnProductCategory()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
                var finishedGoodCategory = await context.Categories.SingleAsync(x => x.Code == "FG");
                finishedGoodCategory.InventoryAccountId = null;
                await context.SaveChangesAsync(CancellationToken.None);
            }

            var result = await SendAsync(CreateOpeningStockCommand());

            result.Succeeded.Should().BeFalse();
            result.Message.Should().Contain("Inventory Account");
        }

        private static PostOpeningStockCommand CreateOpeningStockCommand(decimal quantity = 25, decimal unitCost = 1200, decimal? sellingPrice = null)
        {
            return new PostOpeningStockCommand
            {
                OpeningDate = new DateTime(2026, 6, 17),
                WarehouseId = 1,
                Notes = "Initial stock count",
                Items = new List<OpeningStockLineRequest>
                {
                    new() { ProductVariantId = 200, Quantity = quantity, UnitCost = unitCost, SellingPrice = sellingPrice, BatchNo = "OB-LOT-001" }
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
                SellingPrice = 100
            });

            context.Products.Add(serviceProduct);
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }
}
