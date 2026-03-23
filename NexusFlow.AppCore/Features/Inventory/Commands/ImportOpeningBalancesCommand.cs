using CsvHelper;
using CsvHelper.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NexusFlow.AppCore.Features.Inventory.Commands
{
    public class ImportOpeningBalancesCommand : IRequest<Result<int>>
    {
        public Stream CsvStream { get; set; }
        public int DestinationWarehouseId { get; set; }

        public ImportOpeningBalancesCommand(Stream csvStream, int destinationWarehouseId)
        {
            CsvStream = csvStream;
            DestinationWarehouseId = destinationWarehouseId;
        }
    }

    public class ImportOpeningBalancesHandler : IRequestHandler<ImportOpeningBalancesCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;

        public ImportOpeningBalancesHandler(IErpDbContext context, IJournalService journalService)
        {
            _context = context;
            _journalService = journalService;
        }

        public async Task<Result<int>> Handle(ImportOpeningBalancesCommand request, CancellationToken cancellationToken)
        {
            var equityConfig = await _context.SystemConfigs
                .FirstOrDefaultAsync(c => c.Key == "Account.Equity.OpeningBalance", cancellationToken);

            if (equityConfig == null || !int.TryParse(equityConfig.Value, out int openingBalanceEquityAccountId))
                return Result<int>.Failure("CRITICAL: System Config 'Account.Equity.OpeningBalance' is missing or invalid.");

            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StreamReader(request.CsvStream);
            using var csv = new CsvReader(reader, config);

            var records = csv.GetRecords<LegacyProductCsvRecord>()
                             .Where(r => r.TotalQuantity > 0 && !string.IsNullOrWhiteSpace(r.ItemCode))
                             .ToList();

            if (!records.Any()) return Result<int>.Failure("No valid stock rows found.");

            var skus = records.Select(r => r.ItemCode!.Trim()).Distinct().ToList();
            var dbVariants = await _context.ProductVariants
                .Include(v => v.Product).ThenInclude(p => p.Category)
                .Where(v => skus.Contains(v.SKU))
                .ToDictionaryAsync(v => v.SKU, v => v, cancellationToken);

            var stockLayers = new List<StockLayer>();
            var stockTransactions = new List<StockTransaction>();
            var glGroupings = new Dictionary<int, decimal>();

            string migrationRef = $"MIG-{DateTime.UtcNow:yyyyMMddHHmmss}";
            int importedCount = 0;
            decimal totalCutoverValue = 0;

            using var dbTransaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var row in records)
                {
                    if (!dbVariants.TryGetValue(row.ItemCode!.Trim(), out var variant)) continue;
                    if (variant.Product.Type == ProductType.Service) continue;

                    int inventoryAccountId = variant.Product.Category.InventoryAccountId ?? 0;
                    if (inventoryAccountId == 0) throw new InvalidOperationException($"Category '{variant.Product.Category.Name}' lacks an Inventory Account.");

                    // PERFECTLY MAPPED TO YOUR ENTITY
                    stockLayers.Add(new StockLayer
                    {
                        ProductVariantId = variant.Id,
                        WarehouseId = request.DestinationWarehouseId,
                        BatchNo = migrationRef,
                        InitialQty = row.TotalQuantity,
                        RemainingQty = row.TotalQuantity,
                        UnitCost = row.AverageCost,
                        DateReceived = DateTime.UtcNow,
                        IsExhausted = false // Utilizing the new optimization flag
                    });

                    decimal totalValue = row.TotalQuantity * row.AverageCost;

                    // PERFECTLY MAPPED TO YOUR ENTITY
                    stockTransactions.Add(new StockTransaction
                    {
                        Date = DateTime.UtcNow,
                        ProductVariantId = variant.Id,
                        WarehouseId = request.DestinationWarehouseId,
                        Type = StockTransactionType.Receipt,
                        Qty = row.TotalQuantity,
                        UnitCost = row.AverageCost,
                        TotalValue = totalValue,
                        ReferenceDocNo = migrationRef,
                        Notes = "Legacy System Cutover Opening Balance" // Utilizing the new audit flag
                    });

                    if (!glGroupings.ContainsKey(inventoryAccountId)) glGroupings[inventoryAccountId] = 0;
                    glGroupings[inventoryAccountId] += totalValue;
                    totalCutoverValue += totalValue;
                    importedCount++;
                }

                await _context.StockLayers.AddRangeAsync(stockLayers, cancellationToken);
                await _context.StockTransactions.AddRangeAsync(stockTransactions, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                // --- POST MASSIVE CUTOVER JOURNAL ---
                var journalLines = new List<JournalLineRequest>();
                foreach (var group in glGroupings)
                {
                    journalLines.Add(new JournalLineRequest { AccountId = group.Key, Debit = group.Value, Credit = 0, Note = "Opening Balance Cutover" });
                }
                journalLines.Add(new JournalLineRequest { AccountId = openingBalanceEquityAccountId, Debit = 0, Credit = totalCutoverValue, Note = "Opening Balance Offset" });

                var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                {
                    Date = DateTime.UtcNow,
                    Description = $"Legacy Inventory Migration - {importedCount} items",
                    Module = "Inventory",
                    ReferenceNo = migrationRef,
                    Lines = journalLines
                });

                if (!journalResult.Succeeded) throw new InvalidOperationException($"GL Cutover Failed: {journalResult.Message}");

                await dbTransaction.CommitAsync(cancellationToken);
                return Result<int>.Success(importedCount, $"Migrated {importedCount} lines. Total Value: {totalCutoverValue:C}.");
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Migration Failed: {ex.Message}");
            }
        }
    }
}
