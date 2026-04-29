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
    public class CustomerStatementRowDto
    {
        public string Date { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; } // Increases balance (Invoice)
        public decimal Credit { get; set; } // Decreases balance (Payment, Return)
        public decimal Balance { get; set; } // Running Balance
    }

    public class GetCustomerStatementQuery : IRequest<Result<List<CustomerStatementRowDto>>>
    {
        public int CustomerId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class GetCustomerStatementHandler : IRequestHandler<GetCustomerStatementQuery, Result<List<CustomerStatementRowDto>>>
    {
        private readonly IErpDbContext _context;
        public GetCustomerStatementHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<CustomerStatementRowDto>>> Handle(GetCustomerStatementQuery request, CancellationToken cancellationToken)
        {
            if (request.CustomerId <= 0) return Result<List<CustomerStatementRowDto>>.Failure("Customer ID is required.");

            var endOfDay = request.EndDate.Date.AddDays(1).AddTicks(-1);
            var startOfDay = request.StartDate.Date;

            // 1. CALCULATE OPENING BALANCE (Everything before StartDate)
            decimal openingInvoices = await _context.SalesInvoices
                .Where(i => i.CustomerId == request.CustomerId && i.IsPosted && i.InvoiceDate < startOfDay)
                .SumAsync(i => i.GrandTotal, cancellationToken);

            decimal openingPayments = await _context.PaymentTransactions
                .Where(p => p.CustomerId == request.CustomerId && !p.IsVoided && p.Type == PaymentType.CustomerReceipt && p.Date < startOfDay)
                .SumAsync(p => p.Amount, cancellationToken);

            decimal openingCreditNotes = await _context.CreditNotes
                .Where(c => c.CustomerId == request.CustomerId && c.IsPosted && c.Date < startOfDay)
                .SumAsync(c => c.GrandTotal, cancellationToken);

            decimal runningBalance = openingInvoices - openingPayments - openingCreditNotes;

            var statement = new List<CustomerStatementRowDto>
            {
                new CustomerStatementRowDto
                {
                    Date = startOfDay.ToString("yyyy-MM-dd"),
                    TransactionType = "Opening Balance",
                    ReferenceNo = "-",
                    Description = "Balance Brought Forward",
                    Debit = 0, Credit = 0, Balance = runningBalance
                }
            };

            // 2. FETCH TRANSACTIONS IN DATE RANGE
            var invoices = await _context.SalesInvoices
                .Where(i => i.CustomerId == request.CustomerId && i.IsPosted && i.InvoiceDate >= startOfDay && i.InvoiceDate <= endOfDay)
                .Select(i => new { Date = i.InvoiceDate, Type = "Invoice", Ref = i.InvoiceNumber, Desc = "Sales Invoice", Debit = i.GrandTotal, Credit = 0M })
                .ToListAsync(cancellationToken);

            var payments = await _context.PaymentTransactions
                .Where(p => p.CustomerId == request.CustomerId && !p.IsVoided && p.Type == PaymentType.CustomerReceipt && p.Date >= startOfDay && p.Date <= endOfDay)
                .Select(p => new { Date = p.Date, Type = "Payment Receipt", Ref = p.ReferenceNo, Desc = p.Method.ToString(), Debit = 0M, Credit = p.Amount })
                .ToListAsync(cancellationToken);

            var creditNotes = await _context.CreditNotes
                .Where(c => c.CustomerId == request.CustomerId && c.IsPosted && c.Date >= startOfDay && c.Date <= endOfDay)
                .Select(c => new { Date = c.Date, Type = "Credit Note", Ref = c.CreditNoteNumber, Desc = c.Reason, Debit = 0M, Credit = c.GrandTotal })
                .ToListAsync(cancellationToken);

            // 3. COMBINE, SORT, AND COMPUTE RUNNING BALANCE
            var allTransactions = invoices.Concat(payments).Concat(creditNotes).OrderBy(t => t.Date).ToList();

            foreach (var t in allTransactions)
            {
                runningBalance += t.Debit;  // Invoice increases owed amount
                runningBalance -= t.Credit; // Payment/CN decreases owed amount

                statement.Add(new CustomerStatementRowDto
                {
                    Date = t.Date.ToString("yyyy-MM-dd"),
                    TransactionType = t.Type,
                    ReferenceNo = t.Ref,
                    Description = t.Desc,
                    Debit = t.Debit,
                    Credit = t.Credit,
                    Balance = runningBalance
                });
            }

            return Result<List<CustomerStatementRowDto>>.Success(statement);
        }
    }
}
