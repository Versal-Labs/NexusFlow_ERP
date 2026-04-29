using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Reporting.Queries
{
    public class SupplierStatementRowDto
    {
        public string Date { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Credit { get; set; } // Increases what we owe (Bill)
        public decimal Debit { get; set; } // Decreases what we owe (Payment to supplier)
        public decimal Balance { get; set; } // Running Balance
    }

    public class GetSupplierStatementQuery : IRequest<Result<List<SupplierStatementRowDto>>>
    {
        public int SupplierId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class GetSupplierStatementHandler : IRequestHandler<GetSupplierStatementQuery, Result<List<SupplierStatementRowDto>>>
    {
        private readonly IErpDbContext _context;
        public GetSupplierStatementHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<SupplierStatementRowDto>>> Handle(GetSupplierStatementQuery request, CancellationToken cancellationToken)
        {
            if (request.SupplierId <= 0) return Result<List<SupplierStatementRowDto>>.Failure("Supplier ID is required.");

            var endOfDay = request.EndDate.Date.AddDays(1).AddTicks(-1);
            var startOfDay = request.StartDate.Date;

            // 1. CALCULATE OPENING BALANCE (What we owed before start date)
            decimal openingBills = await _context.SupplierBills
                .Where(b => b.SupplierId == request.SupplierId && b.IsPosted && b.BillDate < startOfDay)
                .SumAsync(b => b.GrandTotal, cancellationToken);

            decimal openingPayments = await _context.PaymentTransactions
                .Where(p => p.SupplierId == request.SupplierId && !p.IsVoided && p.Type == PaymentType.SupplierPayment && p.Date < startOfDay)
                .SumAsync(p => p.Amount, cancellationToken);

            // Assuming you have a DebitNote entity for Supplier Returns. If not, omit this.
            // decimal openingDebitNotes = await _context.DebitNotes.Where(...).SumAsync(...);

            decimal runningBalance = openingBills - openingPayments; // Add - openingDebitNotes if applicable

            var statement = new List<SupplierStatementRowDto>
            {
                new SupplierStatementRowDto
                {
                    Date = startOfDay.ToString("yyyy-MM-dd"),
                    TransactionType = "Opening Balance",
                    ReferenceNo = "-",
                    Description = "Balance Brought Forward",
                    Credit = 0, Debit = 0, Balance = runningBalance
                }
            };

            // 2. TRANSACTIONS IN DATE RANGE
            var bills = await _context.SupplierBills
                .Where(b => b.SupplierId == request.SupplierId && b.IsPosted && b.BillDate >= startOfDay && b.BillDate <= endOfDay)
                .Select(b => new { Date = b.BillDate, Type = "AP Bill", Ref = b.BillNumber, Desc = $"Vendor Inv: {b.SupplierInvoiceNo}", Credit = b.GrandTotal, Debit = 0M })
                .ToListAsync(cancellationToken);

            var payments = await _context.PaymentTransactions
                .Where(p => p.SupplierId == request.SupplierId && !p.IsVoided && p.Type == PaymentType.SupplierPayment && p.Date >= startOfDay && p.Date <= endOfDay)
                .Select(p => new { Date = p.Date, Type = "Payment Made", Ref = p.ReferenceNo, Desc = p.Method.ToString(), Credit = 0M, Debit = p.Amount })
                .ToListAsync(cancellationToken);

            // 3. COMBINE & SORT
            var allTransactions = bills.Concat(payments).OrderBy(t => t.Date).ToList();

            foreach (var t in allTransactions)
            {
                runningBalance += t.Credit; // Bill increases what we owe
                runningBalance -= t.Debit;  // Payment decreases what we owe

                statement.Add(new SupplierStatementRowDto
                {
                    Date = t.Date.ToString("yyyy-MM-dd"),
                    TransactionType = t.Type,
                    ReferenceNo = t.Ref,
                    Description = t.Desc,
                    Credit = t.Credit,
                    Debit = t.Debit,
                    Balance = runningBalance
                });
            }

            return Result<List<SupplierStatementRowDto>>.Success(statement);
        }
    }
}
