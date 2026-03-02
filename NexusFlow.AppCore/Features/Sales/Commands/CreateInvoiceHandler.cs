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
                if (customer.DefaultReceivableAccountId == 0) return Result<int>.Failure("Customer is missing an A/R Account mapping.");

                // 3. GENERATE INVOICE NUMBER (Thread-Safe)
                string invoiceNo = await _sequenceService.GenerateNextNumberAsync("SalesInvoice", cancellationToken);

                var invoice = new SalesInvoice
                {
                    InvoiceNumber = invoiceNo,
                    InvoiceDate = dto.Date,
                    DueDate = dto.DueDate,
                    CustomerId = dto.CustomerId,
                    IsPosted = true,
                    SubTotal = 0,
                    TotalTax = 0,
                    TotalDiscount = 0,
                    GrandTotal = 0
                };

                // Dictionaries to dynamically group Revenue and COGS by the exact Product accounts
                var revenueGroup = new Dictionary<int, decimal>();
                var cogsGroup = new Dictionary<int, decimal>();
                var inventoryAssetGroup = new Dictionary<int, decimal>();

                // Get Tax Rate (Abstracted out for future multi-tax logic)
                decimal vatRate = await _taxService.GetTaxRateAsync("VAT", dto.Date);

                // 4. PROCESS LINES
                foreach (var item in dto.Items)
                {
                    // Fetch Product Variant to know its Type (Stock vs Service) and Financial Bindings
                    var variant = await _context.ProductVariants
                        .Include(v => v.Product)
                        .FirstOrDefaultAsync(v => v.Id == item.ProductVariantId, cancellationToken);

                    if (variant == null) throw new Exception($"Product Variant {item.ProductVariantId} not found.");

                    // Financials
                    decimal lineTotal = (item.Quantity * item.UnitPrice) - item.Discount;
                    decimal taxAmount = lineTotal * (vatRate / 100m);

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
                    invoice.TotalTax += taxAmount;
                    invoice.TotalDiscount += item.Discount;

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
                        int cogsAcc = variant.Product.CogsAccountId ?? 0;
                        int invAcc = variant.Product.InventoryAccountId ?? 0;

                        if (cogsAcc == 0 || invAcc == 0)
                            throw new Exception($"Product '{variant.Product.Name}' is missing COGS or Inventory Asset Accounts.");

                        if (!cogsGroup.ContainsKey(cogsAcc)) cogsGroup[cogsAcc] = 0;
                        cogsGroup[cogsAcc] += actualCogs;

                        if (!inventoryAssetGroup.ContainsKey(invAcc)) inventoryAssetGroup[invAcc] = 0;
                        inventoryAssetGroup[invAcc] += actualCogs;
                    }
                }

                invoice.GrandTotal = invoice.SubTotal + invoice.TotalTax;

                _context.SalesInvoices.Add(invoice);
                await _context.SaveChangesAsync(cancellationToken);

                // 5. POST TO GENERAL LEDGER (Dynamic Routing)
                var journalLines = new List<JournalLineRequest>();

                // A. DEBIT: Customer Accounts Receivable
                journalLines.Add(new JournalLineRequest
                {
                    AccountId = customer.DefaultReceivableAccountId,
                    Debit = invoice.GrandTotal,
                    Credit = 0,
                    Note = $"AR for Invoice {invoiceNo}"
                });

                // B. CREDIT: Revenue Accounts (Dynamically unspooled)
                foreach (var rev in revenueGroup)
                {
                    journalLines.Add(new JournalLineRequest { AccountId = rev.Key, Debit = 0, Credit = rev.Value, Note = $"Revenue - {invoiceNo}" });
                }

                // C. CREDIT: Tax Payable
                if (invoice.TotalTax > 0)
                {
                    var taxConfig = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "Account.Tax.VATPayable");
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
                if (!journalResult.Succeeded) throw new Exception($"GL Posting Failed: {journalResult.Message ?? journalResult.Errors?.FirstOrDefault()}");

                // 6. COMMIT ALL
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(invoice.Id, $"Invoice {invoiceNo} generated successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Invoice Generation Failed: {ex.Message}");
            }
        }
    }
}
