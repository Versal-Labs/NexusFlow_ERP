using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Commands
{
    public record ReceiveProductionCommand : IRequest<Result<string>>
    {
        public DateTime ReceiptDate { get; init; } = DateTime.UtcNow;
        public string IssueReferenceNo { get; init; } = string.Empty; // The Material Issue Note (MIN-0001)
        public int FinishedGoodVariantId { get; init; }
        public int WarehouseId { get; init; }
        public decimal QuantityReceived { get; init; }
        public decimal SubcontractorCharge { get; init; } // The total labor bill for this batch
        public string BatchNo { get; init; } = string.Empty;
    }

    public class ReceiveProductionHandler : IRequestHandler<ReceiveProductionCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequenceService;
        private readonly IFinancialAccountResolver _accountResolver;

        public ReceiveProductionHandler(IErpDbContext context, INumberSequenceService sequenceService, IFinancialAccountResolver accountResolver)
        {
            _context = context;
            _sequenceService = sequenceService;
            _accountResolver = accountResolver;
        }

        public async Task<Result<string>> Handle(ReceiveProductionCommand request, CancellationToken cancellationToken)
        {
            // 1. Calculate the WIP Material Cost by summing the corresponding Issue Note transactions
            var wipMaterialCost = await _context.StockTransactions
                .Where(st => st.ReferenceDocNo == request.IssueReferenceNo && st.Type == StockTransactionType.TransferOut)
                .SumAsync(st => st.TotalValue, cancellationToken);

            if (wipMaterialCost == 0)
                return Result<string>.Failure($"Could not find any issued material costs for Reference No: {request.IssueReferenceNo}. Ensure materials were issued first.");

            // 2. Calculate Final Finished Good Cost
            decimal totalFinishedGoodCost = wipMaterialCost + request.SubcontractorCharge;
            decimal unitCost = totalFinishedGoodCost / request.QuantityReceived;

            string receiptNo = await _sequenceService.GenerateNextNumberAsync("ProductionReceipt", cancellationToken);

            // 3. Create the New Stock Layer for the Finished Good (FIFO Entry point)
            var newStockLayer = new StockLayer
            {
                ProductVariantId = request.FinishedGoodVariantId,
                WarehouseId = request.WarehouseId,
                BatchNo = string.IsNullOrWhiteSpace(request.BatchNo) ? receiptNo : request.BatchNo,
                RemainingQty = request.QuantityReceived,
                UnitCost = unitCost
            };

            await _context.StockLayers.AddAsync(newStockLayer, cancellationToken);

            // 4. Create the Stock Transaction Log
            var stockTransaction = new StockTransaction
            {
                Date = request.ReceiptDate,
                ProductVariantId = request.FinishedGoodVariantId,
                WarehouseId = request.WarehouseId,
                Type = StockTransactionType.ProductionIn, // Correctly mapped to your enum
                Qty = request.QuantityReceived,
                UnitCost = unitCost,
                TotalValue = totalFinishedGoodCost,
                ReferenceDocNo = receiptNo,
                Notes = $"Job Work Receipt. Ref: {request.IssueReferenceNo}. Labor: {request.SubcontractorCharge:C2}"
            };

            await _context.StockTransactions.AddAsync(stockTransaction, cancellationToken);

            // 5. Real-Time GL Posting (3-Way Double Entry)
            int fgInventoryAccountId = await _accountResolver.ResolveAccountIdAsync("FG_INV");
            int wipInventoryAccountId = await _accountResolver.ResolveAccountIdAsync("WIP_INV");
            int apTradeAccountId = await _accountResolver.ResolveAccountIdAsync("AP_TRADE"); // Accrued Labor

            var journalEntry = new JournalEntry
            {
                Date = request.ReceiptDate,
                Description = $"Production Receipt {receiptNo} - Job Work Capitalization",
                Module = "Inventory",
                ReferenceNo = receiptNo,
                TotalAmount = totalFinishedGoodCost,
                Lines = new List<JournalLine>
            {
                // Debit FG Inventory (Asset goes UP)
                new JournalLine { AccountId = fgInventoryAccountId, Debit = totalFinishedGoodCost, Credit = 0 },
                
                // Credit WIP Inventory (Asset goes DOWN by the RM cost)
                new JournalLine { AccountId = wipInventoryAccountId, Debit = 0, Credit = wipMaterialCost },
                
                // Credit Accounts Payable (Liability goes UP for Subcontractor's invoice)
                new JournalLine { AccountId = apTradeAccountId, Debit = 0, Credit = request.SubcontractorCharge }
            }
            };

            await _context.JournalEntries.AddAsync(journalEntry, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<string>.Success(receiptNo, $"Production Receipt {receiptNo} posted. Unit Cost calculated at {unitCost:C2}");
        }
    }
}
