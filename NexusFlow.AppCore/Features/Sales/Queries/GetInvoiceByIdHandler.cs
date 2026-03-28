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
        public decimal GrandTotal { get; set; }
        public List<InvoiceLineDetailsDto> Items { get; set; } = new();
    }

    public class InvoiceLineDetailsDto
    {
        public int ProductVariantId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal InvoicedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
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
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

            if (invoice == null) return Result<InvoiceDetailsDto>.Failure("Invoice not found.");

            var dto = new InvoiceDetailsDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                GrandTotal = invoice.GrandTotal,
                Items = invoice.Items.Select(line => new InvoiceLineDetailsDto
                {
                    ProductVariantId = line.ProductVariantId,
                    Description = line.Description,
                    InvoicedQuantity = line.Quantity,
                    UnitPrice = line.UnitPrice
                }).ToList()
            };

            return Result<InvoiceDetailsDto>.Success(dto);
        }
    }
}
