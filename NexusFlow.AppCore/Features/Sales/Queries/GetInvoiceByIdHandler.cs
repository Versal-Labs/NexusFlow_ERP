using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Queries
{
    // Local DTOs for the Return UI
    public class InvoiceDetailsDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public List<InvoiceLineDetailsDto> Items { get; set; } = new();
    }

    public class InvoiceLineDetailsDto
    {
        public int ProductVariantId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal InvoicedQuantity { get; set; }
        public decimal Discount { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class GetInvoiceByIdQuery : IRequest<Result<InvoiceDetailsDto>>
    {
        public int InvoiceId { get; set; }
    }

    public class GetInvoiceByIdHandler : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDetailsDto>>
    {
        private readonly IErpDbContext _context;

        public GetInvoiceByIdHandler(IErpDbContext context) => _context = context;

        public async Task<Result<InvoiceDetailsDto>> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
        {
            var invoice = await _context.SalesInvoices
                .Include(i => i.Items)
                .Include(i => i.Customer) // Include Customer for Document Viewer
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

            if (invoice == null) return Result<InvoiceDetailsDto>.Failure("Invoice not found.");

            var dto = new InvoiceDetailsDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                CustomerName = invoice.Customer?.Name ?? "Unknown Customer",
                Notes = invoice.Notes,
                SubTotal = invoice.SubTotal,
                TotalTax = invoice.TotalTax,
                TotalDiscount = invoice.TotalDiscount,
                GrandTotal = invoice.GrandTotal,
                AmountPaid = invoice.AmountPaid,
                PaymentStatus = invoice.PaymentStatus.ToString(),
                Items = invoice.Items.Select(line => new InvoiceLineDetailsDto
                {
                    ProductVariantId = line.ProductVariantId,
                    Description = line.Description,
                    InvoicedQuantity = line.Quantity,
                    Discount = line.Discount,
                    UnitPrice = line.UnitPrice,
                    LineTotal = line.LineTotal
                }).ToList()
            };

            return Result<InvoiceDetailsDto>.Success(dto);
        }
    }
}
