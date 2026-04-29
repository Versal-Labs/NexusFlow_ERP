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
                // 1. FETCH ORDER WITH DEEP GRAPH
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
                decimal vatRate = await _taxService.GetTaxRateAsync("VAT", DateTime.UtcNow);

                var invoice = new SalesInvoice
                {
                    InvoiceNumber = invoiceNo,
                    InvoiceDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(30),
                    CustomerId = order.CustomerId,
                    SalesRepId = order.SalesRepId,
                    Notes = $"Converted from Order {order.OrderNumber}. {order.Notes}",
                    ApplyVat = true,
                    IsPosted = true,
                    SubTotal = 0,
                    TotalTax = 0,
                    GrandTotal = 0
                };

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

                    // B. Consume Stock & Group COGS
                    if (product.Type != ProductType.Service)
                    {
                        var stockResult = await _stockService.ConsumeStockAsync(
                            item.ProductVariantId, request.WarehouseId, item.Quantity, invoiceNo, $"Sales Invoice {invoiceNo}");

                        if (!stockResult.Succeeded) throw new InvalidOperationException($"Stock Error for {product.Name}: {stockResult.Message}");

                        decimal actualCogs = stockResult.Data;

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
                        SalesRepId = order.SalesRepId, // Added .Value guard
                        SalesInvoice = invoice,
                        CommissionAmount = totalCommission,
                        Status = CommissionStatus.Unearned
                    });
                }

                // 5. UPDATE ORDER STATUS
                order.Status = SalesOrderStatus.Converted;
                await _context.SaveChangesAsync(cancellationToken);

                // 6. POST TO GENERAL LEDGER
                if (order.Customer.DefaultReceivableAccountId == 0) throw new InvalidOperationException("Customer is missing an A/R Account mapping.");

                var journalLines = new List<JournalLineRequest>
                {
                    new JournalLineRequest { AccountId = order.Customer.DefaultReceivableAccountId, Debit = invoice.GrandTotal, Credit = 0, Note = $"AR for Invoice {invoiceNo}" }
                };

                foreach (var rev in revenueGroup)
                    journalLines.Add(new JournalLineRequest { AccountId = rev.Key, Debit = 0, Credit = rev.Value, Note = $"Revenue - {invoiceNo}" });

                if (invoice.TotalTax > 0)
                {
                    var taxId = await _accountResolver.ResolveAccountIdAsync("Account.Tax.VATPayable", cancellationToken);
                    if (taxId == null) throw new InvalidOperationException("Global Tax Payable account is not configured.");
                    journalLines.Add(new JournalLineRequest { AccountId = taxId, Debit = 0, Credit = invoice.TotalTax, Note = $"VAT - {invoiceNo}" });
                }

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
        // THE NEW TIER-1 COMMISSION ENGINE (PERCENTAGE & FLAT-RATE AWARE)
        // =========================================================================
        private async Task<decimal> CalculateCommissionAsync(int? salesRepId, IEnumerable<SalesOrderItem> items, CancellationToken cancellationToken)
        {
            if (!salesRepId.HasValue) return 0;

            var now = DateTime.UtcNow;
            var activeRules = await _context.CommissionRules
                .AsNoTracking()
                .Where(r => r.IsActive
                         && (!r.EffectiveFrom.HasValue || r.EffectiveFrom <= now)
                         && (!r.EffectiveTo.HasValue || r.EffectiveTo >= now))
                .ToListAsync(cancellationToken);

            if (!activeRules.Any()) return 0;

            decimal totalCommission = 0;

            foreach (var item in items)
            {
                int categoryId = item.ProductVariant.Product.CategoryId;

                var p1 = activeRules.FirstOrDefault(r => r.EmployeeId == salesRepId && r.RuleType == CommissionRuleType.CategoryBased && r.CategoryId == categoryId);
                var p2 = activeRules.FirstOrDefault(r => r.EmployeeId == salesRepId && r.RuleType == CommissionRuleType.GlobalFlatRate);
                var p3 = activeRules.FirstOrDefault(r => r.EmployeeId == null && r.RuleType == CommissionRuleType.CategoryBased && r.CategoryId == categoryId);
                var p4 = activeRules.FirstOrDefault(r => r.EmployeeId == null && r.RuleType == CommissionRuleType.GlobalFlatRate);

                var appliedRule = p1 ?? p2 ?? p3 ?? p4;

                if (appliedRule != null)
                {
                    // ENTERPRISE FIX: Enforcing IsPercentage logic here
                    if (appliedRule.IsPercentage)
                    {
                        totalCommission += item.LineTotal * (appliedRule.CommissionPercentage / 100m);
                    }
                    else
                    {
                        totalCommission += item.Quantity * appliedRule.CommissionPercentage; // Flat Rate per Unit
                    }
                }
            }

            return totalCommission;
        }
    }
}
