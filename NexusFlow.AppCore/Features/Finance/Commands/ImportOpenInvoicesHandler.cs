using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Commands
{
    public class OpenInvoiceImportDto
    {
        public string Type { get; set; } = string.Empty; // "AR" (Customer) or "AP" (Supplier)
        public string PartyName { get; set; } = string.Empty;
        public string DocumentNo { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal OutstandingAmount { get; set; }
    }

    public class ImportOpenInvoicesCommand : IRequest<Result<string>>
    {
        public List<OpenInvoiceImportDto> Invoices { get; set; } = new();
    }

    public class ImportOpenInvoicesHandler : IRequestHandler<ImportOpenInvoicesCommand, Result<string>>
    {
        private readonly IErpDbContext _context;
        private readonly IJournalService _journalService;
        private readonly IFinancialAccountResolver _accountResolver;

        public ImportOpenInvoicesHandler(IErpDbContext context, IJournalService journalService, IFinancialAccountResolver accountResolver)
        {
            _context = context; _journalService = journalService; _accountResolver = accountResolver;
        }

        public async Task<Result<string>> Handle(ImportOpenInvoicesCommand request, CancellationToken cancellationToken)
        {
            if (!request.Invoices.Any()) return Result<string>.Failure("No records to import.");

            int obeAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Equity.OpeningBalance", cancellationToken);
            int arAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Asset.AccountsReceivable", cancellationToken);
            int apAccountId = await _accountResolver.ResolveAccountIdAsync("Account.Liability.AccountsPayable", cancellationToken);

            if (obeAccountId == 0 || arAccountId == 0 || apAccountId == 0)
                return Result<string>.Failure("CRITICAL: AR, AP, or OBE control accounts are missing from System Config.");

            using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            string migrationRef = $"MIG-ARAP-{DateTime.UtcNow:yyyyMMddHHmmss}";

            try
            {
                decimal totalAr = 0;
                decimal totalAp = 0;
                int recordsProcessed = 0;

                // Cache to avoid hitting DB in a loop for every row
                var customers = await _context.Customers.ToDictionaryAsync(c => c.Name.ToUpper(), c => c, cancellationToken);
                var suppliers = await _context.Suppliers.ToDictionaryAsync(s => s.Name.ToUpper(), s => s, cancellationToken);

                foreach (var inv in request.Invoices.Where(i => i.OutstandingAmount > 0))
                {
                    string party = inv.PartyName.Trim().ToUpper();

                    if (inv.Type.ToUpper() == "AR")
                    {
                        // 1. Resolve Customer
                        if (!customers.TryGetValue(party, out var customer))
                        {
                            customer = new Customer { Name = party, Email = "migrated@legacy.com", IsActive = true };
                            _context.Customers.Add(customer);
                            customers[party] = customer;
                        }

                        // 2. Create Stripped-Down Sales Invoice PERFECTLY MAPPED
                        var salesInvoice = new SalesInvoice
                        {
                            Customer = customer,
                            InvoiceNumber = inv.DocumentNo,
                            InvoiceDate = inv.Date,
                            DueDate = inv.Date.AddDays(30), // Defaulting to Net 30
                            SubTotal = inv.OutstandingAmount,
                            TotalTax = 0,
                            TotalDiscount = 0,
                            GrandTotal = inv.OutstandingAmount,
                            AmountPaid = 0,
                            PaymentStatus = InvoicePaymentStatus.Unpaid,
                            IsPosted = true, // Make sure it's recognized as a final document
                            ApplyVat = false, // VAT was handled in legacy system
                            Notes = "Legacy Migration Balance"
                        };
                        _context.SalesInvoices.Add(salesInvoice);
                        totalAr += inv.OutstandingAmount;
                    }
                    else if (inv.Type.ToUpper() == "AP")
                    {
                        // 1. Resolve Supplier
                        if (!suppliers.TryGetValue(party, out var supplier))
                        {
                            supplier = new Supplier { Name = party, Email = "migrated@legacy.com", IsActive = true };
                            _context.Suppliers.Add(supplier);
                            suppliers[party] = supplier;
                        }

                        // 2. Create Stripped-Down Supplier Bill PERFECTLY MAPPED
                        var bill = new SupplierBill
                        {
                            Supplier = supplier,
                            BillNumber = inv.DocumentNo,
                            SupplierInvoiceNo = inv.DocumentNo, // Mapping document to vendor invoice no too
                            BillDate = inv.Date,
                            DueDate = inv.Date.AddDays(30),
                            SubTotal = inv.OutstandingAmount,
                            TaxAmount = 0,
                            GrandTotal = inv.OutstandingAmount,
                            AmountPaid = 0,
                            PaymentStatus = InvoicePaymentStatus.Unpaid,
                            IsPosted = true,
                            ApplyVat = false,
                            Remarks = "Legacy Migration Balance"
                        };
                        _context.SupplierBills.Add(bill);
                        totalAp += inv.OutstandingAmount;
                    }

                    recordsProcessed++;
                }

                await _context.SaveChangesAsync(cancellationToken);

                // 3. POST THE AR/AP CUTOVER JOURNAL
                var journalLines = new List<JournalLineRequest>();

                if (totalAr > 0)
                {
                    journalLines.Add(new JournalLineRequest { AccountId = arAccountId, Debit = totalAr, Credit = 0, Note = "AR Migration Opening Balance" });
                    journalLines.Add(new JournalLineRequest { AccountId = obeAccountId, Debit = 0, Credit = totalAr, Note = "Offset for AR Cutover" });
                }

                if (totalAp > 0)
                {
                    journalLines.Add(new JournalLineRequest { AccountId = apAccountId, Debit = 0, Credit = totalAp, Note = "AP Migration Opening Balance" });
                    journalLines.Add(new JournalLineRequest { AccountId = obeAccountId, Debit = totalAp, Credit = 0, Note = "Offset for AP Cutover" });
                }

                if (journalLines.Any())
                {
                    var jResult = await _journalService.PostJournalAsync(new JournalEntryRequest
                    {
                        Date = DateTime.UtcNow,
                        Description = $"AR/AP Migration Cutover",
                        Module = "Finance",
                        ReferenceNo = migrationRef,
                        Lines = journalLines
                    });
                    if (!jResult.Succeeded) throw new Exception(jResult.Message);
                }

                await transaction.CommitAsync(cancellationToken);
                return Result<string>.Success($"Migrated {recordsProcessed} open records. AR: {totalAr:C}, AP: {totalAp:C}.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<string>.Failure($"AR/AP Migration Failed: {ex.Message}");
            }
        }
    }
}
