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

        public CreateInvoiceHandler(
            IErpDbContext context,
            IStockService stockService,
            ITaxService taxService,
            IJournalService journalService,
            INumberSequenceService sequenceService)
        {
            _context = context;
            _stockService = stockService;
            _taxService = taxService;
            _journalService = journalService;
            _sequenceService = sequenceService;
        }

        public async Task<Result<int>> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Invoice;

            // 1. ATOMIC TRANSACTION: Bind Stock and Finance into one unbreakable operation
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 2. FETCH CUSTOMER & VALIDATE
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Id == dto.CustomerId, cancellationToken);

                if (customer == null) return Result<int>.Failure("Customer not found.");

                // 3. GENERATE INVOICE NUMBER (Thread-Safe)
                string invoiceNo = await _sequenceService.GenerateNextNumberAsync("SalesInvoice", cancellationToken);

                // 4. INITIALIZE INVOICE
                var invoice = new SalesInvoice
                {
                    InvoiceNumber = invoiceNo,
                    InvoiceDate = dto.Date,
                    DueDate = dto.DueDate,
                    CustomerId = dto.CustomerId,
                    SalesRepId = dto.SalesRepId, // Capture Sales Rep
                    Notes = dto.Notes,           // Capture Notes
                    ApplyVat = dto.ApplyVat,     // VAT Toggle
                    IsPosted = !dto.IsDraft,     // Draft vs Posted
                    TotalDiscount = dto.GlobalDiscountAmount, // Absolute global discount
                    SubTotal = 0,
                    TotalTax = 0,
                    GrandTotal = 0
                };

                // Dictionaries to dynamically group Revenue and COGS by the exact Product accounts
                var revenueGroup = new Dictionary<int, decimal>();
                var cogsGroup = new Dictionary<int, decimal>();
                var inventoryAssetGroup = new Dictionary<int, decimal>();

                // Get Tax Rate (Only fetch if VAT is enabled for this invoice)
                decimal vatRate = dto.ApplyVat ? await _taxService.GetTaxRateAsync("VAT", dto.Date) : 0m;

                // 5. PROCESS LINES
                foreach (var item in dto.Items)
                {
                    // Fetch Product Variant to know its Type (Stock vs Service) and Financial Bindings
                    var variant = await _context.ProductVariants
                        .Include(v => v.Product)
                        .FirstOrDefaultAsync(v => v.Id == item.ProductVariantId, cancellationToken);

                    if (variant == null) throw new Exception($"Product Variant {item.ProductVariantId} not found.");

                    // Financials: Line Total reduces by Line Discount
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

                    // IF POSTING (NOT A DRAFT): Prepare Stock and GL Groupings
                    if (!dto.IsDraft)
                    {
                        // Group Revenue based on Product's defined Sales Account
                        int revAccountId = variant.Product.SalesAccountId;
                        if (revAccountId == 0) throw new Exception($"Product '{variant.Product.Name}' is missing a Revenue Account.");

                        if (!revenueGroup.ContainsKey(revAccountId)) revenueGroup[revAccountId] = 0;
                        revenueGroup[revAccountId] += lineTotal;

                        // INVENTORY DEDUCTION (Skip if it's a Service)
                        if (variant.Product.Type != ProductType.Service)
                        {
                            var stockResult = await _stockService.ConsumeStockAsync(
                                item.ProductVariantId,
                                dto.WarehouseId,
                                item.Quantity,
                                invoiceNo
                            );

                            if (!stockResult.Succeeded)
                                throw new Exception($"Stock Error for {variant.Product.Name}: {stockResult.Message}");

                            decimal actualCogs = stockResult.Data; // The actual FIFO cost computed by the engine

                            // Group COGS and Inventory Asset reductions
                            int cogsAcc = variant.Product?.CogsAccountId ?? 0;
                            int invAcc = variant.Product?.InventoryAccountId ?? 0;

                            if (cogsAcc == 0 || invAcc == 0)
                                throw new Exception($"Product '{variant.Product?.Name}' is missing COGS or Inventory Asset Accounts.");

                            if (!cogsGroup.ContainsKey(cogsAcc)) cogsGroup[cogsAcc] = 0;
                            cogsGroup[cogsAcc] += actualCogs;

                            if (!inventoryAssetGroup.ContainsKey(invAcc)) inventoryAssetGroup[invAcc] = 0;
                            inventoryAssetGroup[invAcc] += actualCogs;
                        }
                    }
                }

                // 6. COMPUTE FINAL TOTALS
                decimal taxableAmount = invoice.SubTotal - invoice.TotalDiscount;
                if (taxableAmount < 0) taxableAmount = 0; // Prevent negative taxes

                invoice.TotalTax = taxableAmount * (vatRate / 100m);
                invoice.GrandTotal = taxableAmount + invoice.TotalTax;

                _context.SalesInvoices.Add(invoice);
                await _context.SaveChangesAsync(cancellationToken);

                // ====================================================================
                // 7. POST TO GENERAL LEDGER (ONLY EXECUTED IF NOT A DRAFT)
                // ====================================================================
                if (!dto.IsDraft)
                {
                    if (customer.DefaultReceivableAccountId == 0)
                        throw new Exception("Customer is missing an A/R Account mapping.");

                    var journalLines = new List<JournalLineRequest>();

                    // A. DEBIT: Customer Accounts Receivable
                    journalLines.Add(new JournalLineRequest
                    {
                        AccountId = customer.DefaultReceivableAccountId,
                        Debit = invoice.GrandTotal,
                        Credit = 0,
                        Note = $"AR for Invoice {invoiceNo}"
                    });

                    // B. CREDIT: Revenue Accounts
                    // We must reduce the recognized revenue by the Global Discount to balance the GL
                    var revList = revenueGroup.ToList();
                    if (revList.Any() && invoice.TotalDiscount > 0)
                    {
                        // Subtract global discount from the first revenue group to balance double-entry
                        revList[0] = new KeyValuePair<int, decimal>(revList[0].Key, revList[0].Value - invoice.TotalDiscount);
                    }

                    foreach (var rev in revList)
                    {
                        journalLines.Add(new JournalLineRequest { AccountId = rev.Key, Debit = 0, Credit = rev.Value, Note = $"Revenue - {invoiceNo}" });
                    }

                    // C. CREDIT: Tax Payable
                    if (invoice.TotalTax > 0)
                    {
                        var taxConfig = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "Account.Tax.VATPayable", cancellationToken);
                        if (taxConfig == null) throw new Exception("Global Tax Payable account is not configured.");

                        journalLines.Add(new JournalLineRequest
                        {
                            AccountId = int.Parse(taxConfig.Value),
                            Debit = 0,
                            Credit = invoice.TotalTax,
                            Note = $"VAT - {invoiceNo}"
                        });
                    }

                    // D. PERPETUAL INVENTORY ENTRY (Only if Physical Goods were sold)
                    if (cogsGroup.Any())
                    {
                        // DEBIT: Cost of Goods Sold (Expenses)
                        foreach (var cogs in cogsGroup)
                            journalLines.Add(new JournalLineRequest { AccountId = cogs.Key, Debit = cogs.Value, Credit = 0, Note = $"COGS - {invoiceNo}" });

                        // CREDIT: Inventory Asset (Reduction in warehouse value)
                        foreach (var inv in inventoryAssetGroup)
                            journalLines.Add(new JournalLineRequest { AccountId = inv.Key, Debit = 0, Credit = inv.Value, Note = $"Stock Disp - {invoiceNo}" });
                    }

                    var journalRequest = new JournalEntryRequest
                    {
                        Date = dto.Date,
                        Description = $"Sales Invoice: {invoiceNo} for {customer.Name}",
                        Module = "Sales",
                        ReferenceNo = invoiceNo,
                        Lines = journalLines
                    };

                    var journalResult = await _journalService.PostJournalAsync(journalRequest);
                    if (!journalResult.Succeeded)
                        throw new Exception($"GL Posting Failed: {journalResult.Message ?? journalResult.Errors?.FirstOrDefault()}");
                }

                // 8. COMMIT ALL
                await transaction.CommitAsync(cancellationToken);

                string statusText = dto.IsDraft ? "saved as Draft" : "posted successfully";
                return Result<int>.Success(invoice.Id, $"Invoice {invoiceNo} {statusText}.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Invoice Generation Failed: {ex.Message}");
            }
        }
    }
}
