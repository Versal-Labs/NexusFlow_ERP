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
        private readonly INumberSequenceService _numSequenceService;

        public CreateGrnHandler(INumberSequenceService numSequenceService, IErpDbContext context, IStockService stockService, IJournalService journalService)
        {
            _context = context;
            _stockService = stockService;
            _journalService = journalService;
            _numSequenceService = numSequenceService;
        }

        public async Task<Result<int>> Handle(CreateGrnCommand command, CancellationToken cancellationToken)
        {
            // 1. EXECUTION STRATEGY (Use Transaction for Atomicity)
            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 2. FETCH PO with Deep Graph
                var po = await _context.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Items)
                        .ThenInclude(i => i.ProductVariant)
                            .ThenInclude(pv => pv.Product)
                    .FirstOrDefaultAsync(p => p.Id == command.PurchaseOrderId, cancellationToken);

                if (po == null) return Result<int>.Failure("Purchase Order not found.");
                if (po.Status == PurchaseOrderStatus.Closed) return Result<int>.Failure("PO is already Closed.");

                // 3. GENERATE GRN NUMBER (Concurrency Safe)
                var grnNumber = await _numSequenceService.GenerateNextNumberAsync("GRN", cancellationToken);

                var grn = new GRN
                {
                    GrnNumber = grnNumber,
                    ReceivedDate = command.DateReceived,
                    PurchaseOrderId = command.PurchaseOrderId,
                    WarehouseId = command.WarehouseId,
                    SupplierInvoiceNo = command.SupplierInvoiceNo,
                    CreatedBy = "Admin" // TODO: Wire to IUserService later
                };

                var glGrouping = new Dictionary<int, decimal>();
                decimal grnTotalValue = 0;

                // 4. PROCESS LINES
                foreach (var incomingItem in command.Items)
                {
                    if (incomingItem.QuantityReceived <= 0) continue;

                    var poLine = po.Items.FirstOrDefault(i => i.ProductVariantId == incomingItem.ProductVariantId);
                    if (poLine == null) continue;

                    decimal lineTotal = incomingItem.QuantityReceived * incomingItem.UnitCost;
                    grnTotalValue += lineTotal;

                    // B. Create GRN Line
                    grn.Items.Add(new GRNItem
                    {
                        ProductVariantId = incomingItem.ProductVariantId,
                        QuantityReceived = incomingItem.QuantityReceived,
                        UnitCost = incomingItem.UnitCost,
                        LineTotal = lineTotal
                    });

                    // C. Update PO Status
                    poLine.QuantityReceived += incomingItem.QuantityReceived;

                    // D. Stock Movement
                    await _stockService.ReceiveStockAsync(
                        incomingItem.ProductVariantId,
                        command.WarehouseId,
                        incomingItem.QuantityReceived,
                        incomingItem.UnitCost,
                        grnNumber
                    );

                    // E. Accumulate for GL
                    // Fix: use null-conditional so both operands are nullable and the '??' chain is valid.
                    // InventoryAccountId is int?; Product?.CogsAccountId becomes int? when using null-conditional.
                    int targetAccountId = poLine.ProductVariant?.Product?.InventoryAccountId
                                          ?? poLine.ProductVariant?.Product?.CogsAccountId
                                          ?? 0;

                    if (targetAccountId == 0)
                        throw new Exception($"Product '{poLine.ProductVariant.Product.Name}' is missing an Inventory/Expense Account mapping.");

                    if (!glGrouping.ContainsKey(targetAccountId))
                        glGrouping[targetAccountId] = 0;

                    glGrouping[targetAccountId] += lineTotal;
                }

                if (grnTotalValue == 0)
                    throw new Exception("GRN total value cannot be zero.");

                grn.TotalAmount = grnTotalValue;

                // 5. AUTO-CLOSE PO LOGIC
                bool allReceived = po.Items.All(i => i.QuantityReceived >= (i.QuantityOrdered * 0.99m));
                po.Status = allReceived ? PurchaseOrderStatus.Closed : PurchaseOrderStatus.Partial;

                _context.GRNs.Add(grn);
                await _context.SaveChangesAsync(cancellationToken); // Save to generate IDs

                // 6. FINANCIAL POSTING (Grouped)
                int apAccountId = po.Supplier.DefaultPayableAccountId ?? 0;
                if (apAccountId == 0) throw new Exception($"Supplier {po.Supplier.Name} has no Payable Account configured.");

                var journalLines = new List<JournalLineRequest>
                {
                    // CREDIT Line (Liability)
                    new JournalLineRequest
                    {
                        AccountId = apAccountId,
                        Credit = grnTotalValue,
                        Debit = 0,
                        Note = $"Supplier: {po.Supplier.Name} | Invoice: {command.SupplierInvoiceNo}"
                    }
                };

                // DEBIT Lines (Assets/Expenses grouped by Account)
                foreach (var entry in glGrouping)
                {
                    journalLines.Add(new JournalLineRequest
                    {
                        AccountId = entry.Key,
                        Debit = entry.Value,
                        Credit = 0,
                        Note = $"Stock Receipt - {grnNumber}"
                    });
                }

                // CRITICAL FIX: We must capture the result of the Journal posting
                var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = command.DateReceived,
                    Description = $"GRN: {grnNumber} for PO {po.PoNumber}",
                    Module = "Purchasing",
                    ReferenceNo = grnNumber,
                    Lines = journalLines
                });

                // If the journal fails, throw exception to trigger transaction rollback
                if (!journalResult.Succeeded)
                {
                    throw new Exception(journalResult.Message ?? journalResult.Errors?.FirstOrDefault() ?? "Journal Entry Failed");
                }

                // 7. COMMIT TRANSACTION
                await transaction.CommitAsync(cancellationToken);

                return Result<int>.Success(grn.Id, "GRN processed successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"GRN Failed: {ex.Message}");
            }
        }
    }
}
