using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Orders.Commands
{
    public class ConvertOrderToInvoiceHandler : IRequestHandler<ConvertOrderToInvoiceCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly ITaxService _taxService;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public ConvertOrderToInvoiceHandler(
            IErpDbContext context,
            IStockService stockService,
            ITaxService taxService,
            IJournalService journalService,
            INumberSequenceService sequenceService,
            IFinancialAccountResolver accountResolver)
        {
            _context = context;
            _stockService = stockService;
            _taxService = taxService;
            _journalService = journalService;
            _sequenceService = sequenceService;
            _accountResolver = accountResolver;
        }

        public async Task<Result<int>> Handle(ConvertOrderToInvoiceCommand request, CancellationToken cancellationToken)
        {
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. FETCH ORDER WITH DEEP GRAPH (To resolve Category Posting Groups)
                var order = await _context.SalesOrders
                    .Include(o => o.Customer)
                    .Include(o => o.Items)
                        .ThenInclude(i => i.ProductVariant)
                            .ThenInclude(v => v.Product)
                                .ThenInclude(p => p.Category)
                    .FirstOrDefaultAsync(o => o.Id == request.SalesOrderId, cancellationToken);

                if (order == null) return Result<int>.Failure("Sales Order not found.");
                if (order.Status != SalesOrderStatus.Submitted) return Result<int>.Failure($"Order cannot be converted. Current status is {order.Status}.");

                string invoiceNo = await _sequenceService.GenerateNextNumberAsync("SalesInvoice", cancellationToken);
                decimal vatRate = await _taxService.GetTaxRateAsync("VAT", DateTime.UtcNow); // Assuming standard VAT applies

                var invoice = new SalesInvoice
                {
                    InvoiceNumber = invoiceNo,
                    InvoiceDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(30), // Configurable term
                    CustomerId = order.CustomerId,
                    SalesRepId = order.SalesRepId,
                    Notes = $"Converted from Order {order.OrderNumber}. {order.Notes}",
                    ApplyVat = true, // Configurable per customer
                    IsPosted = true,
                    SubTotal = 0,
                    TotalTax = 0,
                    GrandTotal = 0
                };

                // GL Groupings for the Journal Entry
                var revenueGroup = new Dictionary<int, decimal>();
                var cogsGroup = new Dictionary<int, decimal>();
                var inventoryAssetGroup = new Dictionary<int, decimal>();

                // 2. PROCESS ITEMS & CONSUME FIFO STOCK
                foreach (var item in order.Items)
                {
                    var product = item.ProductVariant.Product;
                    var category = product.Category;

                    invoice.Items.Add(new SalesInvoiceItem
                    {
                        ProductVariantId = item.ProductVariantId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Discount = item.Discount,
                        LineTotal = item.LineTotal,
                        Description = product.Name
                    });

                    invoice.SubTotal += item.LineTotal;

                    // A. Group Revenue
                    int revAccountId = category.SalesAccountId ?? 0;
                    if (revAccountId == 0) throw new InvalidOperationException($"Category '{category.Name}' missing Sales Revenue Account.");
                    if (!revenueGroup.ContainsKey(revAccountId)) revenueGroup[revAccountId] = 0;
                    revenueGroup[revAccountId] += item.LineTotal;

                    // B. Consume Stock & Group COGS (If Physical Item)
                    if (product.Type != ProductType.Service)
                    {
                        var stockResult = await _stockService.ConsumeStockAsync(
                            item.ProductVariantId,
                            request.WarehouseId,
                            item.Quantity,
                            invoiceNo,
                            $"Sales Invoice {invoiceNo}");

                        if (!stockResult.Succeeded) throw new InvalidOperationException($"Stock Error for {product.Name}: {stockResult.Message}");

                        decimal actualCogs = stockResult.Data; // Exact FIFO Cost

                        int cogsAcc = category.CogsAccountId ?? 0;
                        int invAcc = category.InventoryAccountId ?? 0;

                        if (cogsAcc == 0 || invAcc == 0) throw new InvalidOperationException($"Category '{category.Name}' missing COGS or Inventory Account.");

                        if (!cogsGroup.ContainsKey(cogsAcc)) cogsGroup[cogsAcc] = 0;
                        cogsGroup[cogsAcc] += actualCogs;

                        if (!inventoryAssetGroup.ContainsKey(invAcc)) inventoryAssetGroup[invAcc] = 0;
                        inventoryAssetGroup[invAcc] += actualCogs;
                    }
                }

                // 3. FINALIZE INVOICE MATH
                invoice.TotalTax = invoice.SubTotal * (vatRate / 100m);
                invoice.GrandTotal = invoice.SubTotal + invoice.TotalTax;

                _context.SalesInvoices.Add(invoice);

                // 4. THE COMMISSION SPECIFICITY ENGINE
                decimal totalCommission = await CalculateCommissionAsync(order.SalesRepId, order.Items, cancellationToken);

                if (totalCommission > 0)
                {
                    _context.CommissionLedgers.Add(new CommissionLedger
                    {
                        SalesRepId = order.SalesRepId,
                        SalesInvoice = invoice, // EF Core resolves the ID upon save
                        CommissionAmount = totalCommission,
                        Status = CommissionStatus.Unearned // Awaiting Customer Payment
                    });
                }

                // 5. UPDATE ORDER STATUS
                order.Status = SalesOrderStatus.Converted;
                await _context.SaveChangesAsync(cancellationToken); // Save to generate InvoiceId

                // 6. POST TO GENERAL LEDGER
                if (order.Customer.DefaultReceivableAccountId == 0)
                    throw new InvalidOperationException("Customer is missing an A/R Account mapping.");

                var journalLines = new List<JournalLineRequest>
                {
                    // DEBIT: Accounts Receivable
                    new JournalLineRequest { AccountId = order.Customer.DefaultReceivableAccountId, Debit = invoice.GrandTotal, Credit = 0, Note = $"AR for Invoice {invoiceNo}" }
                };

                // CREDIT: Revenue Accounts
                foreach (var rev in revenueGroup)
                    journalLines.Add(new JournalLineRequest { AccountId = rev.Key, Debit = 0, Credit = rev.Value, Note = $"Revenue - {invoiceNo}" });

                // CREDIT: Tax Payable
                if (invoice.TotalTax > 0)
                {
                    var taxConfig = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "Account.Tax.VATPayable", cancellationToken);
                    if (taxConfig == null) throw new InvalidOperationException("Global Tax Payable account is not configured.");
                    journalLines.Add(new JournalLineRequest { AccountId = int.Parse(taxConfig.Value), Debit = 0, Credit = invoice.TotalTax, Note = $"VAT - {invoiceNo}" });
                }

                // DEBIT COGS / CREDIT INVENTORY
                foreach (var cogs in cogsGroup)
                    journalLines.Add(new JournalLineRequest { AccountId = cogs.Key, Debit = cogs.Value, Credit = 0, Note = $"COGS - {invoiceNo}" });

                foreach (var inv in inventoryAssetGroup)
                    journalLines.Add(new JournalLineRequest { AccountId = inv.Key, Debit = 0, Credit = inv.Value, Note = $"Stock Disp - {invoiceNo}" });

                var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = invoice.InvoiceDate,
                    Description = $"Sales Invoice: {invoiceNo} (Converted from {order.OrderNumber})",
                    Module = "Sales",
                    ReferenceNo = invoiceNo,
                    Lines = journalLines
                });

                if (!journalResult.Succeeded) throw new InvalidOperationException($"GL Posting Failed: {journalResult.Message}");

                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(invoice.Id, $"Order successfully converted to Invoice {invoiceNo}. Commission liability recorded: {totalCommission:C}.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Conversion Failed: {ex.Message}");
            }
        }

        // =========================================================================
        // THE COMMISSION SPECIFICITY MATRIX ENGINE
        // =========================================================================
        private async Task<decimal> CalculateCommissionAsync(int salesRepId, IEnumerable<SalesOrderItem> items, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            // Fetch all currently active rules
            var activeRules = await _context.CommissionRules
                .AsNoTracking()
                .Where(r => r.IsActive
                         && (!r.ValidFrom.HasValue || r.ValidFrom <= now)
                         && (!r.ValidTo.HasValue || r.ValidTo >= now))
                .ToListAsync(cancellationToken);

            if (!activeRules.Any()) return 0;

            decimal totalCommission = 0;

            foreach (var item in items)
            {
                int categoryId = item.ProductVariant.Product.CategoryId;
                decimal percentageToApply = 0;

                // PRIORITY 1: Specific Rep + Specific Category
                var p1 = activeRules.FirstOrDefault(r => r.EmployeeId == salesRepId && r.RuleType == CommissionRuleType.CategoryBased && r.CategoryId == categoryId);

                // PRIORITY 2: Specific Rep + Global Flat Rate
                var p2 = activeRules.FirstOrDefault(r => r.EmployeeId == salesRepId && r.RuleType == CommissionRuleType.GlobalFlatRate);

                // PRIORITY 3: All Reps + Specific Category
                var p3 = activeRules.FirstOrDefault(r => r.EmployeeId == null && r.RuleType == CommissionRuleType.CategoryBased && r.CategoryId == categoryId);

                // PRIORITY 4: All Reps + Global Flat Rate
                var p4 = activeRules.FirstOrDefault(r => r.EmployeeId == null && r.RuleType == CommissionRuleType.GlobalFlatRate);

                // Resolution
                if (p1 != null) percentageToApply = p1.CommissionPercentage;
                else if (p2 != null) percentageToApply = p2.CommissionPercentage;
                else if (p3 != null) percentageToApply = p3.CommissionPercentage;
                else if (p4 != null) percentageToApply = p4.CommissionPercentage;

                if (percentageToApply > 0)
                {
                    totalCommission += item.LineTotal * (percentageToApply / 100m);
                }
            }

            return totalCommission;
        }
    }
}
