using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Treasury.Queries
{
    // --- DTOs ---
    public class ReceiptDetailsDto
    {
        public int Id { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Method { get; set; }
        public bool IsVoided { get; set; }
        public ChequeDetailsDto? ChequeDetails { get; set; }
        public List<ReceiptAllocationDto> Allocations { get; set; } = new();
    }

    public class ChequeDetailsDto
    {
        public string BankName { get; set; } = string.Empty;
        public string ChequeNumber { get; set; } = string.Empty;
        public DateTime ChequeDate { get; set; }
    }

    public class ReceiptAllocationDto
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal InvoiceTotal { get; set; }
        public decimal AmountAllocated { get; set; }
    }

    // --- QUERY & HANDLER ---
    public class GetReceiptByIdQuery : IRequest<Result<ReceiptDetailsDto>>
    {
        public int ReceiptId { get; set; }
    }

    public class GetReceiptByIdHandler : IRequestHandler<GetReceiptByIdQuery, Result<ReceiptDetailsDto>>
    {
        private readonly IErpDbContext _context;

        public GetReceiptByIdHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<ReceiptDetailsDto>> Handle(GetReceiptByIdQuery request, CancellationToken cancellationToken)
        {
            var receipt = await _context.PaymentTransactions
                    .Include(p => p.Allocations)
                        .ThenInclude(a => a.SalesInvoice)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == request.ReceiptId, cancellationToken);

            // FIX: Must check for null BEFORE accessing receipt.CustomerId!
            if (receipt == null) return Result<ReceiptDetailsDto>.Failure("Receipt not found.");

            string customerName = "Unknown";
            if (receipt.CustomerId.HasValue)
            {
                var customer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == receipt.CustomerId.Value, cancellationToken);
                if (customer != null) customerName = customer.Name;
            }

            var dto = new ReceiptDetailsDto
            {
                Id = receipt.Id,
                ReferenceNo = receipt.ReferenceNo,
                Date = receipt.Date,
                CustomerName = customerName,
                Remarks = receipt.Remarks,
                Amount = receipt.Amount,
                Method = (int)receipt.Method,
                IsVoided = receipt.IsVoided,

                Allocations = receipt.Allocations.Select(a => new ReceiptAllocationDto
                {
                    InvoiceNumber = a.SalesInvoice.InvoiceNumber,
                    InvoiceDate = a.SalesInvoice.InvoiceDate,
                    InvoiceTotal = a.SalesInvoice.GrandTotal,
                    AmountAllocated = a.AmountAllocated
                }).ToList()
            };

            // If it was a Cheque, fetch the details from the Vault
            if (receipt.Method == PaymentMethod.Cheque)
            {
                // TIER-1 FIX: Traverse the normalized schema up to the Bank table
                var cheque = await _context.ChequeRegisters
                    .Include(c => c.BankBranch)
                        .ThenInclude(b => b.Bank)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.OriginalReceiptId == receipt.Id, cancellationToken);

                if (cheque != null)
                {
                    // Format as "Bank Name - Branch Name"
                    string fullBankName = (cheque.BankBranch != null && cheque.BankBranch.Bank != null)
                        ? $"{cheque.BankBranch.Bank.Name} - {cheque.BankBranch.BranchName}"
                        : "Unknown Bank";

                    dto.ChequeDetails = new ChequeDetailsDto
                    {
                        BankName = fullBankName,
                        ChequeNumber = cheque.ChequeNumber,
                        ChequeDate = cheque.ChequeDate
                    };
                }
            }

            return Result<ReceiptDetailsDto>.Success(dto);
        }
    }
}
