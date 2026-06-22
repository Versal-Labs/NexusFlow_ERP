using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Features.Inventory.ProductionOrders;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Enums;
using Xunit;

namespace NexusFlow.IntegrationTests
{
    public class ProductionOrderControlTests : TestBase
    {
        [Fact]
        public async Task PartialReceipts_AllocateEachWipCostOnlyOnce()
        {
            var orderId = await CreateAndReleaseOrderAsync(10);
            int componentId;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<Infrastructure.ErpDbContext>();
                componentId = await context.ProductionOrderComponents.Where(x => x.ProductionOrderId == orderId).Select(x => x.Id).SingleAsync();
                context.StockLayers.Add(new StockLayer
                {
                    ProductVariantId = 100, WarehouseId = 1, BatchNo = "FIFO-1", DateReceived = DateTime.UtcNow.AddDays(-5),
                    InitialQty = 15, RemainingQty = 15, UnitCost = 100, IsExhausted = false
                });
                await context.SaveChangesAsync();
            }

            var issue = await SendAsync(new IssueProductionMaterialsCommand
            {
                ProductionOrderId = orderId, IssueDate = DateTime.UtcNow.Date,
                Lines = new() { new ProductionMaterialLineRequest(componentId, 15) }
            });
            Assert.True(issue.Succeeded, issue.Message);

            for (var receiptIndex = 0; receiptIndex < 2; receiptIndex++)
            {
                var receipt = await SendAsync(new ReceiveProductionOrderCommand
                {
                    ProductionOrderId = orderId, ReceiptDate = DateTime.UtcNow.Date,
                    AcceptedQuantity = 5, SewingCharge = 100,
                    Consumptions = new() { new ProductionConsumptionRequest(componentId, 7.5m, 0, 0, 0) }
                });
                Assert.True(receipt.Succeeded, receipt.Message);
            }

            using var verifyScope = _scopeFactory.CreateScope();
            var verify = verifyScope.ServiceProvider.GetRequiredService<Infrastructure.ErpDbContext>();
            var component = await verify.ProductionOrderComponents.SingleAsync(x => x.Id == componentId);
            var receipts = await verify.ProductionReceipts.Where(x => x.ProductionOrderId == orderId).ToListAsync();
            var finishedStock = await verify.StockLayers.Where(x => x.ProductVariantId == 200).SumAsync(x => x.RemainingQty);

            Assert.Equal(1500m, component.IssuedCost);
            Assert.Equal(1500m, component.ConsumedCost);
            Assert.Equal(0m, component.UnallocatedWipCost);
            Assert.Equal(1500m, receipts.Sum(x => x.MaterialCostCapitalized));
            Assert.Equal(1700m, receipts.Sum(x => x.FinishedGoodsCost));
            Assert.Equal(10m, finishedStock);
        }

        [Fact]
        public async Task ClosedFinancialPeriod_BlocksTransactionBeforeNumberGeneration()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<Infrastructure.ErpDbContext>();
                var period = await context.FinancialPeriods.SingleAsync();
                period.IsClosed = true;
                await context.SaveChangesAsync();
            }

            var result = await SendAsync(new CreateProductionOrderCommand
            {
                OrderDate = DateTime.UtcNow.Date, ContractorId = 1, FinishedGoodVariantId = 200,
                BillOfMaterialId = 1, SourceWarehouseId = 1, DestinationWarehouseId = 2, TargetQuantity = 10
            });

            Assert.False(result.Succeeded);
            Assert.Equal("financial_period_not_open", result.Code);
            using var verifyScope = _scopeFactory.CreateScope();
            var verify = verifyScope.ServiceProvider.GetRequiredService<Infrastructure.ErpDbContext>();
            Assert.Equal(1, await verify.NumberSequences.Where(x => x.Module == "ProductionOrder").Select(x => x.NextNumber).SingleAsync());
        }

        private async Task<int> CreateAndReleaseOrderAsync(decimal quantity)
        {
            var create = await SendAsync(new CreateProductionOrderCommand
            {
                OrderDate = DateTime.UtcNow.Date, ContractorId = 1, FinishedGoodVariantId = 200,
                BillOfMaterialId = 1, SourceWarehouseId = 1, DestinationWarehouseId = 2, TargetQuantity = quantity
            });
            Assert.True(create.Succeeded, create.Message);
            var release = await SendAsync(new ReleaseProductionOrderCommand(create.Data, DateTime.UtcNow.Date));
            Assert.True(release.Succeeded, release.Message);
            return create.Data;
        }
    }
}
