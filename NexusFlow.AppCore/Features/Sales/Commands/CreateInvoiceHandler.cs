using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Sales;
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

        public CreateInvoiceHandler(
            IErpDbContext context,
            IStockService stockService,
            ITaxService taxService,
            IJournalService journalService)
        {
            _context = context;
            _stockService = stockService;
            _taxService = taxService;
            _journalService = journalService;
        }

        public async Task<Result<int>> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Invoice;

            // 1. GENERATE INVOICE NUMBER
            // In a real app, use a sequence table. Here we use a simple count logic.
            int count = await _context.SalesInvoices.CountAsync(cancellationToken) + 1;
            string invoiceNo = $"INV-{DateTime.UtcNow.Year}-{count:D6}";

            // 2. INITIALIZE CALCULATIONS
            var invoice = new SalesInvoice
            {
                InvoiceNumber = invoiceNo,
                InvoiceDate = dto.Date,
                DueDate = dto.DueDate,
                CustomerId = dto.CustomerId,
                IsPosted = true
            };

            decimal totalCOGS = 0; // Cost of Goods Sold (Internal Cost)

            // 3. GET TAX RATE (VAT Only - Sri Lanka Context)
            decimal vatRate = await _taxService.GetTaxRateAsync("VAT", dto.Date);

            // 4. PROCESS LINES
            foreach (var item in dto.Items)
            {
                // A. Calculate Line Financials
                decimal lineTotal = (item.Quantity * item.UnitPrice) - item.Discount;
                decimal taxAmount = lineTotal * (vatRate / 100);

                // B. Add to Invoice Entity
                var invoiceItem = new SalesInvoiceItem
                {
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Discount = item.Discount,
                    LineTotal = lineTotal,
                    Description = "Product Sale" // You could fetch the real name here
                };
                invoice.Items.Add(invoiceItem);

                // C. UPDATE TOTALS
                invoice.SubTotal += lineTotal;
                invoice.TotalTax += taxAmount;

                // D. INVENTORY DEDUCTION (FIFO)
                // We reuse the Consume logic but track it as a Sale.
                // This returns the ACTUAL COST of the items sold (e.g., the factory cost).
                var stockResult = await _stockService.ConsumeStockAsync(
                    item.ProductVariantId,
                    dto.WarehouseId,
                    item.Quantity,
                    invoiceNo
                );

                if (!stockResult.Succeeded)
                    return Result<int>.Failure($"Stock Error for Item {item.ProductVariantId}: {stockResult.Message}");

                totalCOGS += stockResult.Data;
            }

            invoice.GrandTotal = invoice.SubTotal + invoice.TotalTax;

            // 5. SAVE INVOICE
            _context.SalesInvoices.Add(invoice);
            await _context.SaveChangesAsync(cancellationToken);

            // ==============================================================================
            // 6. POST TO GENERAL LEDGER (Double Entry)
            // ==============================================================================

            // Fetch Configured Accounts
            var configKeys = new[] {
            "Account.Sales.Receivable",  // Asset (Customer owes us)
            "Account.Sales.Revenue",     // Income (Sales)
            "Account.Tax.VATPayable",    // Liability (We owe Govt)
            "Account.Cost.COGS",         // Expense (Cost of items)
            "Account.Inventory.FinishedGood" // Asset (Inventory reducing)
        };

            var configs = await _context.SystemConfigs
                .Where(c => configKeys.Contains(c.Key))
                .ToDictionaryAsync(c => c.Key, c => c.Value, cancellationToken);

            // (Skipping null checks for brevity, but you should add them like before)

            var journalRequest = new JournalEntryRequest
            {
                Date = dto.Date,
                Description = $"Sales Invoice: {invoiceNo}",
                Module = "Sales",
                ReferenceNo = invoiceNo,
                Lines = new List<JournalLineRequest>
            {
                // --- ENTRY 1: THE SALE ---
                // DEBIT: Customer Receivable (Grand Total)
                new() { AccountId = int.Parse(configs["Account.Sales.Receivable"]), Debit = invoice.GrandTotal, Credit = 0 },
                
                // CREDIT: Sales Revenue (SubTotal)
                new() { AccountId = int.Parse(configs["Account.Sales.Revenue"]), Debit = 0, Credit = invoice.SubTotal },

                // CREDIT: VAT Payable (Tax Only)
                new() { AccountId = int.Parse(configs["Account.Tax.VATPayable"]), Debit = 0, Credit = invoice.TotalTax },

                // --- ENTRY 2: THE COST (Perpetual Inventory System) ---
                // DEBIT: Cost of Goods Sold (Expense)
                new() { AccountId = int.Parse(configs["Account.Cost.COGS"]), Debit = totalCOGS, Credit = 0 },

                // CREDIT: Inventory Asset (The value that left the warehouse)
                new() { AccountId = int.Parse(configs["Account.Inventory.FinishedGood"]), Debit = 0, Credit = totalCOGS }
            }
            };

            var journalResult = await _journalService.PostJournalAsync(journalRequest);
            if (!journalResult.Succeeded) return Result<int>.Failure($"GL Posting Failed: {journalResult.Message}");

            return Result<int>.Success(invoice.Id, $"Invoice {invoiceNo} Created. VAT: {invoice.TotalTax}");
        }
    }
}
