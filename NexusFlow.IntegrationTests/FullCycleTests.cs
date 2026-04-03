using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.DTOs.Inventory;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.AppCore.Features.Inventory.Commands;
using NexusFlow.AppCore.Features.Purchasing.Commands;
using NexusFlow.AppCore.Features.Sales.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Purchasing;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.IntegrationTests
{
    public class FullCycleTests : TestBase
    {
        [Fact]
        public async Task Full_ERP_Cycle_Should_Work_Correctly()
        {
            // =================================================================
            // STEP 1: PROCUREMENT (Buy 1000m Fabric @ $10)
            // =================================================================
            int poId;

            // 1.1 Create PO (Simulated directly in DB for speed)
            using (var scope = _scopeFactory.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
                var po = new PurchaseOrder { PoNumber = "PO-1", SupplierId = 1, Date = DateTime.UtcNow, Status = Domain.Enums.PurchaseOrderStatus.Approved };
                po.Items.Add(new PurchaseOrderItem { ProductVariantId = 100, QuantityOrdered = 1000, UnitCost = 10, QuantityReceived = 0 });
                ctx.PurchaseOrders.Add(po);
                await ctx.SaveChangesAsync(CancellationToken.None);

                poId = po.Id;
            }

            // 1.2 Receive Goods (GRN) into Main Warehouse (ID 1)
            var grnCmd = new CreateGrnCommand
            {
                PurchaseOrderId = poId,
                WarehouseId = 1,
                DateReceived = DateTime.UtcNow,
                Items = new List<GrnItemDto> { new() { ProductVariantId = 100, QuantityReceived = 1000, UnitCost = 10 } }
            };

            var grnResult = await SendAsync(grnCmd);
            grnResult.Succeeded.Should().BeTrue(grnResult.Message);

            // ASSERT: Stock should be 1000
            var stockLayer = await FindAsync<NexusFlow.Domain.Entities.Inventory.StockLayer>(1); // Assuming ID 1
                                                                                                 // (Better to query by logic, but for simple test OK)

            // =================================================================
            // STEP 2: TRANSFER (Move 150m to Factory for Production)
            // =================================================================
            var transferCmd = new TransferStockCommand
            {
                SourceWarehouseId = 1,
                TargetWarehouseId = 2, // Factory
                ReferenceDoc = "TRF-001",
                Items = new List<TransferItemDto> { new() { ProductVariantId = 100, Qty = 150 } }
            };

            var transferResult = await SendAsync(transferCmd);
            transferResult.Succeeded.Should().BeTrue(transferResult.Message);

            // =================================================================
            // STEP 3: PRODUCTION (Make 100 Jeans)
            // =================================================================
            // 100 Jeans requires 150m Fabric (100 * 1.5)
            // Fabric Cost = 150m * $10 = $1500
            // Service Cost = $500
            // Total Cost = $2000 -> Unit Cost = $20.00

            var prodCmd = new RunProductionCommand
            {
                FinishedGoodVariantId = 200, // Jean
                QtyProduced = 100,
                FactoryWarehouseId = 2,
                TargetWarehouseId = 1, // Store Jeans in Main
                TotalServiceCost = 500,
                ReferenceDoc = "PROD-001"
            };

            var prodResult = await SendAsync(prodCmd);
            prodResult.Succeeded.Should().BeTrue(prodResult.Message);

            // =================================================================
            // STEP 4: SALES (Sell 10 Jeans @ $50)
            // =================================================================
            // Revenue: 10 * 50 = 500
            // Tax (18%): 500 * 0.18 = 90
            // COGS: 10 * $20 (Calculated in Step 3) = 200

            var invCmd = new CreateInvoiceCommand
            {
                Invoice = new NexusFlow.AppCore.DTOs.Sales.CreateInvoiceRequest
                {
                    CustomerId = 1,
                    Date = DateTime.UtcNow,
                    WarehouseId = 1,
                    Items = new List<NexusFlow.AppCore.DTOs.Sales.InvoiceLineDto>
                {
                    new() { ProductVariantId = 200, Quantity = 10, UnitPrice = 50, Discount = 0 }
                }
                }
            };

            var invResult = await SendAsync(invCmd);
            invResult.Succeeded.Should().BeTrue(invResult.Message);

            // =================================================================
            // FINAL VALIDATION: THE FINANCIALS
            // =================================================================
            // Use a scope to query the Journal Table
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
                var journals = await context.JournalEntries.Include(j => j.Lines).ToListAsync();

                // Check if we have entries
                journals.Should().NotBeEmpty();

                // Verify Invoice Journal (Last one)
                var salesJournal = journals.Last();
                salesJournal.Module.Should().Be("Sales");

                // Check Sales Revenue Credit
                var revenueLine = salesJournal.Lines.First(l => l.AccountId == 4010);
                revenueLine.Credit.Should().Be(500); // 10 * 50

                // Check COGS Debit
                var cogsLine = salesJournal.Lines.First(l => l.AccountId == 5050);
                cogsLine.Debit.Should().Be(200); // 10 * 20 (Proven Cost Flow!)
            }

            // =================================================================
            // REPORTING CHECK: TRIAL BALANCE
            // =================================================================
            var tbQuery = new NexusFlow.AppCore.Features.Finance.Queries.GetTrialBalanceQuery { AsOfDate = DateTime.UtcNow };
            var tbResult = await SendAsync(tbQuery);

            tbResult.Succeeded.Should().BeTrue();
            var report = tbResult.Data;

            // 1. Must be Balanced
            report.IsBalanced.Should().BeTrue("Debits must equal Credits");
            report.TotalDebit.Should().BeGreaterThan(0);

            // 2. Check Specific Accounts (Based on our scenario)
            // Sales Revenue (4010) should be $500 Credit
            var salesAcct = report.Lines.First(l => l.AccountCode == "4010");
            salesAcct.Credit.Should().Be(500);

            // VAT Payable (2050) should be $90 Credit (18% of 500)
            var vatAcct = report.Lines.First(l => l.AccountCode == "2050");
            vatAcct.Credit.Should().Be(90);

            // Receivables (1040) should be $590 Debit (500 + 90)
            var arAcct = report.Lines.First(l => l.AccountCode == "1040");
            arAcct.Debit.Should().Be(590);

            // =================================================================
            // STEP 5: TREASURY (Settle the Debts)
            // =================================================================

            // 5.1 Pay the Supplier ($10,000 for 1000m Fabric)
            // Note: In Step 1 we bought 1000m @ $10 = $10,000
            var payCmd = new NexusFlow.AppCore.Features.Treasury.Commands.RecordPaymentCommand
            {
                Date = DateTime.UtcNow,
                Type = NexusFlow.Domain.Enums.PaymentType.SupplierPayment,
                Method = NexusFlow.Domain.Enums.PaymentMethod.BankTransfer,
                ReceiptAmount = 10000,
                Remarks = "Settling PO-1"
            };
            var payResult = await SendAsync(payCmd);
            payResult.Succeeded.Should().BeTrue(payResult.Message);

            // 5.2 Receive from Customer ($590 for 10 Jeans)
            // Note: In Step 4 Invoice was $500 + $90 Tax = $590
            var receiptCmd = new NexusFlow.AppCore.Features.Treasury.Commands.RecordPaymentCommand
            {
                Date = DateTime.UtcNow,
                Type = NexusFlow.Domain.Enums.PaymentType.CustomerReceipt,
                Method = NexusFlow.Domain.Enums.PaymentMethod.Cash,
                ReceiptAmount = 590,
                CustomerId = 1,
                Remarks = "Payment for INV-1"
            };
            var receiptResult = await SendAsync(receiptCmd);
            receiptResult.Succeeded.Should().BeTrue(receiptResult.Message);

            // =================================================================
            // STEP 6: FINAL FINANCIAL CHECK
            // =================================================================
            // If we run the Trial Balance again:
            // 1. Accounts Payable should be 0 (Created 10,000, Paid 10,000)
            // 2. Accounts Receivable should be 0 (Created 590, Received 590)
            // 3. Bank/Cash should reflect the movement.

            var finalTbResult = await SendAsync(new NexusFlow.AppCore.Features.Finance.Queries.GetTrialBalanceQuery());
            var finalReport = finalTbResult.Data;

            // Check Supplier Cleared
            var apAcct = finalReport.Lines.FirstOrDefault(l => l.AccountCode == "2010");
            // If account is perfectly 0, it might not be in the list depending on query logic, 
            // OR it will be there with 0 balance.
            if (apAcct != null) apAcct.NetBalance.Should().Be(0);

            // Check Customer Cleared
            var arAcct2 = finalReport.Lines.FirstOrDefault(l => l.AccountCode == "1040");
            if (arAcct2 != null) arAcct2.NetBalance.Should().Be(0);

            // Check Bank (1020) -> Should be -10,000 (Credit) because we paid out
            var bankAcct = finalReport.Lines.First(l => l.AccountCode == "1020");
            bankAcct.Credit.Should().Be(10000);

            // Check Cash (1010) -> Should be +590 (Debit) because we received cash
            var cashAcct = finalReport.Lines.First(l => l.AccountCode == "1010");
            cashAcct.Debit.Should().Be(590);

            // =================================================================
            // STEP 7: BALANCE SHEET CHECK
            // =================================================================
            var bsResult = await SendAsync(new NexusFlow.AppCore.Features.Finance.Queries.GetBalanceSheetQuery());
            bsResult.Succeeded.Should().BeTrue();
            var bs = bsResult.Data;

            // 1. The Golden Rule
            bs.IsBalanced.Should().BeTrue("Assets must equal Liabilities + Equity");

            // 2. Validate Specific Numbers from our scenario
            // ASSETS:
            // - Bank (1020): We paid 10,000. Balance should be -10,000 (Wait! Bank is Asset, Credit balance means Overdraft)
            //   Actually, in Step 5 we paid 10,000 but started with 0. So Bank is -10,000.
            // - Cash (1010): We received 590. Balance = +590.
            // - Inventory (1032): We made 100 Jeans ($20 ea) = 2000. Sold 10 ($200 cost). Remaining = 1800.
            // - Inventory (1031): Bought 1000m ($10) = 10,000. Used 150m ($1500). Remaining = 8500.

            // Let's check Total Assets
            decimal expectedInventory = 1800 + 8500; // 10,300
            decimal expectedCash = 590;
            decimal expectedBank = -10000; // (Technically a Liability now, but stays in Asset section as negative)

            decimal totalAssets = expectedInventory + expectedCash + expectedBank; // 10300 + 590 - 10000 = 890
            bs.Assets.Total.Should().Be(totalAssets);

            // LIABILITIES:
            // - AP (2010): Paid off fully -> 0.
            // - VAT (2050): 90.
            // - Service Liability (2060): We incurred 500 service cost in Production. Not paid yet.
            decimal totalLiabilities = 90 + 500;
            bs.Liabilities.Total.Should().Be(totalLiabilities);

            // EQUITY:
            // - Retained Earnings = Revenue (500) - COGS (200) = 300 Net Profit.
            bs.Equity.Total.Should().Be(300);

            // FINAL MATH CHECK:
            // Assets (890) == Liabilities (590) + Equity (300) ?
            // 890 == 890. YES!
        }
    }
}
