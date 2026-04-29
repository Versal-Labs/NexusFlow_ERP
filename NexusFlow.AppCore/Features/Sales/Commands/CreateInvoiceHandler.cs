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

namespace NexusFlow.AppCore.Features.Sales.Commands
{
    public class CreateInvoiceHandler : IRequestHandler<CreateInvoiceCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly ITaxService _taxService;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public CreateInvoiceHandler(
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

        public async Task<Result<int>> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Invoice;
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == dto.CustomerId, cancellationToken);
                if (customer == null) return Result<int>.Failure("Customer not found.");

                string invoiceNo = await _sequenceService.GenerateNextNumberAsync("SalesInvoice", cancellationToken);

                var invoice = new SalesInvoice
                {
                    InvoiceNumber = invoiceNo,
                    CustomerPoNumber = dto.CustomerPoNumber,
                    InvoiceDate = dto.Date,
                    DueDate = dto.DueDate,
                    CustomerId = dto.CustomerId,
                    SalesRepId = dto.SalesRepId,
                    Notes = dto.Notes,
                    ApplyVat = dto.ApplyVat,
                    IsPosted = !dto.IsDraft,
                    TotalDiscount = dto.GlobalDiscountAmount,
                    SubTotal = 0,
                    TotalTax = 0,
                    GrandTotal = 0,
                };

                var revenueGroup = new Dictionary<int, decimal>();
                var cogsGroup = new Dictionary<int, decimal>();
                var inventoryAssetGroup = new Dictionary<int, decimal>();

                decimal vatRate = dto.ApplyVat ? await _taxService.GetTaxRateAsync("VAT", dto.Date) : 0m;

                // =======================================================================
                // ENTERPRISE ENGINE: Pre-load active commission rules for the Matrix
                // =======================================================================
                decimal totalCommission = 0;
                List<CommissionRule> activeRules = new();

                if (!dto.IsDraft && dto.SalesRepId.HasValue)
                {
                    var now = DateTime.UtcNow;
                    activeRules = await _context.CommissionRules.AsNoTracking()
                        .Where(r => r.IsActive
                                 && (!r.EffectiveFrom.HasValue || r.EffectiveFrom <= now)
                                 && (!r.EffectiveTo.HasValue || r.EffectiveTo >= now))
                        .ToListAsync(cancellationToken);
                }

                foreach (var item in dto.Items)
                {
                    var variant = await _context.ProductVariants
                        .Include(v => v.Product).ThenInclude(p => p.Category)
                        .FirstOrDefaultAsync(v => v.Id == item.ProductVariantId, cancellationToken);

                    if (variant == null) throw new InvalidOperationException($"Product Variant {item.ProductVariantId} not found.");

                    decimal lineTotal = (item.Quantity * item.UnitPrice) - item.Discount;

                    invoice.Items.Add(new SalesInvoiceItem
                    {
                        ProductVariantId = item.ProductVariantId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Discount = item.Discount,
                        LineTotal = lineTotal,
                        Description = variant.Product.Name
                    });

                    invoice.SubTotal += lineTotal;

                    if (!dto.IsDraft)
                    {
                        // 1. Group Revenue
                        int revAccountId = variant.Product?.Category?.SalesAccountId ?? 0;
                        if (revAccountId == 0) throw new Exception($"Product '{variant.Product.Name}' is missing a Revenue Account.");
                        if (!revenueGroup.ContainsKey(revAccountId)) revenueGroup[revAccountId] = 0;
                        revenueGroup[revAccountId] += lineTotal;

                        // 2. Execute Commission Matrix
                        if (dto.SalesRepId.HasValue && activeRules.Any())
                        {
                            int categoryId = variant.Product.CategoryId;
                            int repId = dto.SalesRepId.Value;

                            var p1 = activeRules.FirstOrDefault(r => r.EmployeeId == repId && r.RuleType == CommissionRuleType.CategoryBased && r.CategoryId == categoryId);
                            var p2 = activeRules.FirstOrDefault(r => r.EmployeeId == repId && r.RuleType == CommissionRuleType.GlobalFlatRate);
                            var p3 = activeRules.FirstOrDefault(r => r.EmployeeId == null && r.RuleType == CommissionRuleType.CategoryBased && r.CategoryId == categoryId);
                            var p4 = activeRules.FirstOrDefault(r => r.EmployeeId == null && r.RuleType == CommissionRuleType.GlobalFlatRate);

                            var appliedRule = p1 ?? p2 ?? p3 ?? p4;

                            if (appliedRule != null)
                            {
                                if (appliedRule.IsPercentage)
                                    totalCommission += lineTotal * (appliedRule.CommissionPercentage / 100m);
                                else
                                    totalCommission += item.Quantity * appliedRule.CommissionPercentage; // Value per Unit
                            }
                        }

                        // 3. Inventory Deduction
                        if (variant.Product.Type != ProductType.Service)
                        {
                            var stockResult = await _stockService.ConsumeStockAsync(item.ProductVariantId, dto.WarehouseId, item.Quantity, invoiceNo);
                            if (!stockResult.Succeeded) throw new Exception($"Stock Error for {variant.Product.Name}: {stockResult.Message}");

                            decimal actualCogs = stockResult.Data;
                            int cogsAcc = variant.Product?.Category?.CogsAccountId ?? 0;
                            int invAcc = variant.Product?.Category?.InventoryAccountId ?? 0;

                            if (cogsAcc == 0 || invAcc == 0) throw new Exception($"Product '{variant.Product?.Name}' is missing COGS or Inventory Asset Accounts.");
                            if (!cogsGroup.ContainsKey(cogsAcc)) cogsGroup[cogsAcc] = 0;
                            cogsGroup[cogsAcc] += actualCogs;
                            if (!inventoryAssetGroup.ContainsKey(invAcc)) inventoryAssetGroup[invAcc] = 0;
                            inventoryAssetGroup[invAcc] += actualCogs;
                        }
                    }
                }

                decimal taxableAmount = invoice.SubTotal - invoice.TotalDiscount;
                if (taxableAmount < 0) taxableAmount = 0;
                invoice.TotalTax = taxableAmount * (vatRate / 100m);
                invoice.GrandTotal = taxableAmount + invoice.TotalTax;

                _context.SalesInvoices.Add(invoice);

                // RECORD EARNED COMMISSION LEDGER
                if (!dto.IsDraft && totalCommission > 0 && dto.SalesRepId.HasValue)
                {
                    _context.CommissionLedgers.Add(new CommissionLedger
                    {
                        SalesRepId = dto.SalesRepId.Value,
                        SalesInvoice = invoice,
                        CommissionAmount = totalCommission,
                        Status = CommissionStatus.Unearned // Unearned until customer pays the invoice
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);

                // GL POSTING (Same as before)
                if (!dto.IsDraft)
                {
                    if (customer.DefaultReceivableAccountId == 0) throw new Exception("Customer is missing an A/R Account mapping.");

                    var journalLines = new List<JournalLineRequest> {
                        new JournalLineRequest { AccountId = customer.DefaultReceivableAccountId, Debit = invoice.GrandTotal, Credit = 0, Note = $"AR for Invoice {invoiceNo}" }
                    };

                    var revList = revenueGroup.ToList();
                    if (revList.Any() && invoice.TotalDiscount > 0)
                        revList[0] = new KeyValuePair<int, decimal>(revList[0].Key, revList[0].Value - invoice.TotalDiscount);

                    foreach (var rev in revList) journalLines.Add(new JournalLineRequest { AccountId = rev.Key, Debit = 0, Credit = rev.Value, Note = $"Revenue - {invoiceNo}" });
                    if (invoice.TotalTax > 0)
                    {
                        int taxPayableAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Tax.VATPayable", cancellationToken);
                        journalLines.Add(new JournalLineRequest { AccountId = taxPayableAccountId, Debit = 0, Credit = invoice.TotalTax, Note = $"VAT - {invoiceNo}" });
                    }
                    if (cogsGroup.Any())
                    {
                        foreach (var cogs in cogsGroup) journalLines.Add(new JournalLineRequest { AccountId = cogs.Key, Debit = cogs.Value, Credit = 0, Note = $"COGS - {invoiceNo}" });
                        foreach (var inv in inventoryAssetGroup) journalLines.Add(new JournalLineRequest { AccountId = inv.Key, Debit = 0, Credit = inv.Value, Note = $"Stock Disp - {invoiceNo}" });
                    }

                    var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                    {
                        Date = dto.Date,
                        Description = $"Sales Invoice: {invoiceNo} for {customer.Name}",
                        Module = "Sales",
                        ReferenceNo = invoiceNo,
                        Lines = journalLines
                    });

                    if (!journalResult.Succeeded) throw new Exception($"GL Posting Failed: {journalResult.Message ?? journalResult.Errors?.FirstOrDefault()}");
                }

                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(invoice.Id, $"Invoice {invoiceNo} posted. Commission accrued: {totalCommission:C}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Invoice Generation Failed: {ex.Message}");
            }
        }
    }
}
