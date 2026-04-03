using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.CreditNotes.Queries
{
    // ==========================================
    // 1. DATATABLE GRID LIST QUERY
    // ==========================================
    public class CreditNoteGridDto
    {
        public int Id { get; set; }
        public string CreditNoteNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string OriginalInvoice { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
    }

    public class GetCreditNotesQuery : IRequest<Result<List<CreditNoteGridDto>>>
    {
        public int? CustomerId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class GetCreditNotesHandler : IRequestHandler<GetCreditNotesQuery, Result<List<CreditNoteGridDto>>>
    {
        private readonly IErpDbContext _context;
        public GetCreditNotesHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<CreditNoteGridDto>>> Handle(GetCreditNotesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.CreditNotes
                .Include(c => c.Customer)
                .Include(c => c.SalesInvoice)
                .AsNoTracking().AsQueryable();

            if (request.CustomerId.HasValue) query = query.Where(c => c.CustomerId == request.CustomerId);
            if (request.StartDate.HasValue) query = query.Where(c => c.Date >= request.StartDate.Value);
            if (request.EndDate.HasValue) query = query.Where(c => c.Date <= request.EndDate.Value);

            var list = await query.OrderByDescending(c => c.Id).Select(c => new CreditNoteGridDto
            {
                Id = c.Id,
                CreditNoteNumber = c.CreditNoteNumber,
                Date = c.Date,
                CustomerName = c.Customer.Name,
                OriginalInvoice = c.SalesInvoice.InvoiceNumber,
                GrandTotal = c.GrandTotal
            }).ToListAsync(cancellationToken);

            return Result<List<CreditNoteGridDto>>.Success(list);
        }
    }

    // ==========================================
    // 2. DEEP FETCH FOR VIEW MODAL
    // ==========================================
    public class GetCreditNoteByIdQuery : IRequest<Result<object>>
    {
        public int Id { get; set; }
    }

    public class GetCreditNoteByIdHandler : IRequestHandler<GetCreditNoteByIdQuery, Result<object>>
    {
        private readonly IErpDbContext _context;
        public GetCreditNoteByIdHandler(IErpDbContext context) => _context = context;

        public async Task<Result<object>> Handle(GetCreditNoteByIdQuery request, CancellationToken cancellationToken)
        {
            var cn = await _context.CreditNotes
                .Include(c => c.Customer)
                .Include(c => c.SalesInvoice)
                .Include(c => c.Items).ThenInclude(i => i.ProductVariant)
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            if (cn == null) return Result<object>.Failure("Credit Note not found.");

            // Anonymous object mapping for rapid UI binding
            var data = new
            {
                cn.Id,
                cn.CreditNoteNumber,
                cn.Date,
                cn.Reason,
                CustomerName = cn.Customer.Name,
                OriginalInvoice = cn.SalesInvoice.InvoiceNumber,
                cn.SubTotal,
                cn.TotalTax,
                cn.GrandTotal,
                Items = cn.Items.Select(i => new
                {
                    Description = i.ProductVariant.Name,
                    Sku = i.ProductVariant.SKU,
                    Qty = i.ReturnedQuantity,
                    Price = i.UnitPrice,
                    Total = i.LineTotal
                }).ToList()
            };

            return Result<object>.Success(data);
        }
    }
}
