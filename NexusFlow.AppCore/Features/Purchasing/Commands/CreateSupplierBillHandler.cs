using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Commands
{
    public class CreateSupplierBillCommand : IRequest<Result<int>>
    {
        public CreateSupplierBillRequest Bill { get; set; } = null!;
    }

    public class CreateSupplierBillHandler : IRequestHandler<CreateSupplierBillCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly INumberSequenceService _sequenceService;
        private readonly ITaxService _taxService;
        private readonly IFinancialAccountResolver _financialAccountResolver;

        public CreateSupplierBillHandler(IFinancialAccountResolver financialAccountResolver, IErpDbContext context, IJournalService journalService, INumberSequenceService sequenceService, ITaxService taxService)
        {
            _context = context;
            _journalService = journalService;
            _sequenceService = sequenceService;
            _taxService = taxService;
            _financialAccountResolver = financialAccountResolver;
        }

        public async Task<Result<int>> Handle(CreateSupplierBillCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Bill;
            if (!dto.Items.Any()) return Result<int>.Failure("Bill must contain at least one line item.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == dto.SupplierId, cancellationToken);
                if (supplier == null) return Result<int>.Failure("Supplier not found.");

                string billNo = await _sequenceService.GenerateNextNumberAsync("SupplierBill", cancellationToken);

                var bill = new SupplierBill
                {
                    BillNumber = billNo,
                    SupplierInvoiceNo = dto.SupplierInvoiceNo,
                    BillDate = dto.BillDate,
                    DueDate = dto.DueDate,
                    SupplierId = dto.SupplierId,
                    Remarks = dto.Remarks,
                    ApplyVat = dto.ApplyVat,
                    IsPosted = !dto.IsDraft,
                    SubTotal = 0,
                    TaxAmount = 0,
                    GrandTotal = 0,
                };

                decimal vatRate = dto.ApplyVat ? await _taxService.GetTaxRateAsync("VAT", dto.BillDate) : 0m;
                var debitGrouping = new Dictionary<int, decimal>();

                int unbilledAccountId = await _financialAccountResolver.ResolveAccountIdAsync("Account.Purchasing.UnbilledReceipts", cancellationToken);

                if (unbilledAccountId == null)
                    throw new Exception("Global 'Unbilled Receipts' (GRN Clearing) liability account is not configured in SystemConfigs.");

                foreach (var item in dto.Items)
                {
                    if (item.ProductVariantId == null && item.ExpenseAccountId == null)
                        throw new Exception("Line item must be either a Product or a Direct Expense.");

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
                        int targetDebitAccount = 0;

                        if (item.ProductVariantId.HasValue)
                        {
                            if (unbilledAccountId == 0) throw new Exception("Unbilled Receipts account is not configured in System Configs.");
                            targetDebitAccount = unbilledAccountId; // Clear the GRN Liability
                        }
                        else if (item.ExpenseAccountId.HasValue)
                        {
                            targetDebitAccount = item.ExpenseAccountId.Value; // Direct Expense (e.g., Freight)
                        }

                        if (!debitGrouping.ContainsKey(targetDebitAccount)) debitGrouping[targetDebitAccount] = 0;
                        debitGrouping[targetDebitAccount] += lineTotal;
                    }
                }

                bill.TaxAmount = bill.SubTotal * (vatRate / 100m);
                bill.GrandTotal = bill.SubTotal + bill.TaxAmount;

                _context.SupplierBills.Add(bill);
                if (dto.LinkedGrnIds.Any())
                {
                    var linkedGrns = await _context.GRNs
                        .Include(g => g.PurchaseOrder)
                        .Where(g => dto.LinkedGrnIds.Contains(g.Id))
                        .ToListAsync(cancellationToken);

                    foreach (var grn in linkedGrns)
                    {
                        if (grn.IsBilled) throw new Exception($"GRN {grn.GrnNumber} has already been billed.");
                        if (grn.PurchaseOrder.SupplierId != dto.SupplierId) throw new Exception($"GRN {grn.GrnNumber} belongs to a different supplier.");

                        grn.IsBilled = true;
                        // grn.SupplierBillId = bill.Id; // EF Core will auto-map this if setup correctly, or set it after SaveChanges
                        bill.Grns.Add(grn);
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

                // --- POST TO GENERAL LEDGER ---
                if (!dto.IsDraft)
                {
                    int apAccountId = supplier.DefaultPayableAccountId ?? 0;
                    if (apAccountId == 0) throw new Exception("Supplier does not have a Default Payable Account assigned.");

                    var journalLines = new List<JournalLineRequest>();

                    // CREDIT: Accounts Payable (The Debt)
                    journalLines.Add(new JournalLineRequest { AccountId = apAccountId, Credit = bill.GrandTotal, Debit = 0, Note = $"AP Bill - {billNo}" });

                    // DEBIT: Unbilled Receipts OR Direct Expenses
                    foreach (var debit in debitGrouping)
                    {
                        journalLines.Add(new JournalLineRequest { AccountId = debit.Key, Debit = debit.Value, Credit = 0, Note = $"Bill Lines - {billNo}" });
                    }

                    // DEBIT: Input Tax Receivable
                    if (bill.TaxAmount > 0)
                    {
                        var taxId = await _financialAccountResolver.ResolveAccountIdAsync("Account.Tax.VATReceivable", cancellationToken);
                        if (taxId == null) throw new Exception("Input VAT (Receivable) account is not configured in System Configs.");

                        journalLines.Add(new JournalLineRequest { AccountId = taxId, Debit = bill.TaxAmount, Credit = 0, Note = $"Input VAT - {billNo}" });
                    }

                    var journalResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                    {
                        Date = bill.BillDate,
                        Description = $"Supplier Bill: {billNo} | Ref: {bill.SupplierInvoiceNo}",
                        Module = "Purchasing",
                        ReferenceNo = billNo,
                        Lines = journalLines
                    });

                    if (!journalResult.Succeeded) throw new Exception($"GL Posting Failed: {journalResult.Message}");
                }

                await transaction.CommitAsync(cancellationToken);
                return Result<int>.Success(bill.Id, $"Supplier Bill {billNo} {(dto.IsDraft ? "saved as draft" : "posted to GL")}.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<int>.Failure($"Bill Generation Failed: {ex.Message}");
            }
        }
    }
}
