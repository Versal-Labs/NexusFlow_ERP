using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Commands
{
    public class CreateGrnHandler : IRequestHandler<CreateGrnCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IStockService _stockService;
        private readonly IJournalService _journalService;

        public CreateGrnHandler(IErpDbContext context, IStockService stockService, IJournalService journalService)
        {
            _context = context;
            _stockService = stockService;
            _journalService = journalService;
        }

        public async Task<Result<int>> Handle(CreateGrnCommand command, CancellationToken cancellationToken)
        {
            // 1. VALIDATE PO
            var po = await _context.PurchaseOrders
                .Include(p => p.Items)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == command.PurchaseOrderId, cancellationToken);

            if (po == null) return Result<int>.Failure("Purchase Order not found.");
            if (po.Status == PurchaseOrderStatus.Closed) return Result<int>.Failure("PO is already Closed.");

            // 2. CREATE GRN HEADER
            int grnCount = await _context.GRNs.CountAsync(cancellationToken) + 1;
            string grnNumber = $"GRN-{DateTime.UtcNow.Year}-{grnCount:D6}";

            var grn = new GRN
            {
                GrnNumber = grnNumber,
                ReceivedDate = command.DateReceived,
                PurchaseOrderId = command.PurchaseOrderId,
                WarehouseId = command.WarehouseId,
                SupplierInvoiceNo = command.SupplierInvoiceNo
            };

            decimal grnTotalValue = 0;

            // 3. PROCESS ITEMS
            foreach (var incomingItem in command.Items)
            {
                // Find matching PO Line
                var poLine = po.Items.FirstOrDefault(i => i.ProductVariantId == incomingItem.ProductVariantId);
                if (poLine == null) continue; // Skip items not in PO

                // A. Create GRN Line
                var grnLine = new GRNItem
                {
                    ProductVariantId = incomingItem.ProductVariantId,
                    QuantityReceived = incomingItem.QuantityReceived,
                    UnitCost = incomingItem.UnitCost,
                    LineTotal = incomingItem.QuantityReceived * incomingItem.UnitCost
                };
                grn.Items.Add(grnLine);

                // B. Update PO Status (Track how much we received)
                poLine.QuantityReceived += incomingItem.QuantityReceived;

                // C. Add to Inventory (The Stock Layer)
                // This enables FIFO tracking for this specific batch
                await _stockService.ReceiveStockAsync(
                    incomingItem.ProductVariantId,
                    command.WarehouseId,
                    incomingItem.QuantityReceived,
                    incomingItem.UnitCost,
                    grnNumber // Traceability: This batch comes from this GRN
                );

                grnTotalValue += grnLine.LineTotal;
            }

            grn.TotalAmount = grnTotalValue;

            // Auto-Close PO if fully received (Simplified Logic)
            if (po.Items.All(i => i.QuantityReceived >= i.QuantityOrdered))
            {
                po.Status = PurchaseOrderStatus.Closed;
            }

            _context.GRNs.Add(grn);
            await _context.SaveChangesAsync(cancellationToken);

            // ==============================================================================
            // 4. FINANCIAL POSTING (Debit Inventory, Credit Supplier)
            // ==============================================================================

            // A. Get Configured Accounts
            // We need: "Inventory Asset" and "Accounts Payable"
            var configKeys = new[] { "Account.Inventory.RawMaterial", "Account.Liability.TradeCreditors" };
            var configs = await _context.SystemConfigs
                .Where(c => configKeys.Contains(c.Key))
                .ToDictionaryAsync(c => c.Key, c => c.Value, cancellationToken);

            // Fallback: Use Supplier's default account if configured, else System Config
            int apAccountId = po.Supplier.DefaultPayableAccountId ??
                              (configs.ContainsKey("Account.Liability.TradeCreditors") ? int.Parse(configs["Account.Liability.TradeCreditors"]) : 0);

            int inventoryAccountId = configs.ContainsKey("Account.Inventory.RawMaterial") ? int.Parse(configs["Account.Inventory.RawMaterial"]) : 0;

            if (apAccountId == 0 || inventoryAccountId == 0)
            {
                // Fail gracefully - Save GRN but warn about GL
                return Result<int>.Success(grn.Id, "GRN Saved, but GL Posting Failed (Missing Account Config).");
            }

            // B. Post Journal
            var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
            {
                Date = command.DateReceived,
                Description = $"Goods Received: {grnNumber} (PO: {po.PoNumber})",
                Module = "Purchasing",
                ReferenceNo = grnNumber,
                Lines = new List<JournalLineRequest>
            {
                // DEBIT: Inventory (Asset Increases)
                new() { AccountId = inventoryAccountId, Debit = grnTotalValue, Credit = 0 },

                // CREDIT: Accounts Payable (Liability Increases)
                new() { AccountId = apAccountId, Debit = 0, Credit = grnTotalValue, Note = $"Supplier: {po.Supplier.Name}" }
            }
            });

            return Result<int>.Success(grn.Id, "GRN Created, Stock Updated, and Financials Posted.");
        }
    }
}
