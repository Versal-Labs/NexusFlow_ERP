using MediatR;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Shared.Wrapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Commands
{
    public class UpdateSupplierBillCommand : IRequest<Result<int>>
    {
        public CreateSupplierBillRequest Bill { get; set; } = null!;
    }

    public class UpdateSupplierBillHandler : IRequestHandler<UpdateSupplierBillCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly ITaxService _taxService;
        private readonly IFinancialAccountResolver _financialAccountResolver;

        public UpdateSupplierBillHandler(IFinancialAccountResolver financialAccountResolver, IErpDbContext context, IJournalService journalService, ITaxService taxService)
        {
            _context = context;
            _journalService = journalService;
            _taxService = taxService;
            _financialAccountResolver = financialAccountResolver;
        }

        public async Task<Result<int>> Handle(UpdateSupplierBillCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Bill;
            if (!dto.Items.Any()) return Result<int>.Failure("Bill must contain at least one line item.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Fetch the existing Bill with its Lines and linked GRNs
                var bill = await _context.SupplierBills
                    .Include(b => b.Supplier)
                    .Include(b => b.Items)
                    .Include(b => b.Grns)
                    .FirstOrDefaultAsync(b => b.Id == dto.Id, cancellationToken);

                if (bill == null) return Result<int>.Failure("Supplier Bill not found.");

                // Tier-1 Guard: Cannot edit posted bills
                if (bill.IsPosted) return Result<int>.Failure("Posted bills cannot be edited. You must void them instead.");

                // 2. Release currently linked GRNs back to the wild
                foreach (var oldGrn in bill.Grns)
                {
                    oldGrn.IsBilled = false;
                }
                bill.Grns.Clear();

                // 3. Wipe old items
                _context.SupplierBillItems.RemoveRange(bill.Items);

                // 4. Update Header
                bill.SupplierInvoiceNo = dto.SupplierInvoiceNo;
                bill.BillDate = dto.BillDate;
                bill.DueDate = dto.DueDate;
                bill.SupplierId = dto.SupplierId;
                bill.Remarks = dto.Remarks;
                bill.ApplyVat = dto.ApplyVat;
                bill.IsPosted = !dto.IsDraft;
                bill.SubTotal = 0;
                bill.TaxAmount = 0;
                bill.GrandTotal = 0;

                decimal vatRate = dto.ApplyVat ? await _taxService.GetTaxRateAsync("VAT", dto.BillDate) : 0m;
                var debitGrouping = new Dictionary<int, decimal>();

                int unbilledAccountId = await _financialAccountResolver.ResolveAccountIdAsync("Account.Purchasing.UnbilledReceipts", cancellationToken);

                // 5. Rebuild Items & Calculate Totals
                foreach (var item in dto.Items)
                {
                    decimal lineTotal = item.Quantity * item.UnitPrice;
                    bill.SubTotal += lineTotal;

                    bill.Items.Add(new SupplierBillItem
                    {
                        ProductVariantId = item.ProductVariantId,
                        ExpenseAccountId = item.ExpenseAccountId,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineTotal = lineTotal
                    });

                    if (!dto.IsDraft)
                    {
                        int targetDebitAccount = item.ProductVariantId.HasValue ? unbilledAccountId : (item.ExpenseAccountId ?? 0);
                        if (targetDebitAccount == 0) throw new Exception("Invalid account mapping for bill line.");

                        if (!debitGrouping.ContainsKey(targetDebitAccount)) debitGrouping[targetDebitAccount] = 0;
                        debitGrouping[targetDebitAccount] += lineTotal;
                    }
                }

                bill.TaxAmount = bill.SubTotal * (vatRate / 100m);
                bill.GrandTotal = bill.SubTotal + bill.TaxAmount;

                // 6. Link NEW selected GRNs
                if (dto.LinkedGrnIds.Any())
                {
                    var linkedGrns = await _context.GRNs
                        .Where(g => dto.LinkedGrnIds.Contains(g.Id))
                        .ToListAsync(cancellationToken);

                    foreach (var grn in linkedGrns)
                    {
                        if (grn.IsBilled) throw new Exception($"GRN {grn.GrnNumber} is already billed elsewhere.");
                        grn.IsBilled = true;
                        bill.Grns.Add(grn);
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

                // 7. POST TO GENERAL LEDGER (If transitioning from Draft -> Posted)
                if (!dto.IsDraft)
                {
                    int apAccountId = bill.Supplier.DefaultPayableAccountId ?? 0;
                    if (apAccountId == 0) throw new Exception("Supplier missing Default Payable Account.");

                    var journalLines = new List<JournalLineRequest>
                {
                    new JournalLineRequest { AccountId = apAccountId, Credit = bill.GrandTotal, Debit = 0, Note = $"AP Bill - {bill.BillNumber}" }
                };

                    foreach (var debit in debitGrouping)
                    {
                        journalLines.Add(new JournalLineRequest { AccountId = debit.Key, Debit = debit.Value, Credit = 0, Note = $"Bill Lines - {bill.BillNumber}" });
                    }

                    if (bill.TaxAmount > 0)
                    {
                        var taxId = await _financialAccountResolver.ResolveAccountIdAsync("Account.Tax.VATReceivable", cancellationToken);
                        journalLines.Add(new JournalLineRequest { AccountId = taxId, Debit = bill.TaxAmount, Credit = 0, Note = $"Input VAT - {bill.BillNumber}" });
                    }

                    var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                    {
                        Date = bill.BillDate,
                        Description = $"Supplier Bill: {bill.BillNumber} | Ref: {bill.SupplierInvoiceNo}",
                        Module = "Purchasing",
                        ReferenceNo = bill.BillNumber,
                        Lines = journalLines
                    });

                    if (!journalResult.Succeeded) throw new Exception($"GL Posting Failed: {journalResult.Message}");
                }

                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(bill.Id, $"Supplier Bill {bill.BillNumber} updated successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Update Failed: {ex.Message}");
            }
        }
    }
}
