using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.DebitNotes.Queries
{
    // ==========================================
    // 1. DATATABLE GRID LIST QUERY
    // ==========================================
    public class DebitNoteGridDto
    {
        public int Id { get; set; }
        public string DebitNoteNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
    }

    public class GetDebitNotesQuery : IRequest<Result<List<DebitNoteGridDto>>>
    {
        public int? SupplierId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class GetDebitNotesHandler : IRequestHandler<GetDebitNotesQuery, Result<List<DebitNoteGridDto>>>
    {
        private readonly IErpDbContext _context;
        public GetDebitNotesHandler(IErpDbContext context) => _context = context;

        public async Task<Result<List<DebitNoteGridDto>>> Handle(GetDebitNotesQuery request, CancellationToken cancellationToken)
        {
            // Assuming you tag Debit Notes via a Type or store them in a DebitNotes table
            // For this example, assuming _context.DebitNotes exists. 
            var query = _context.SupplierBills
                .Where(b => b.BillNumber.StartsWith("DN-")) // Or however you identify them
                .Include(c => c.Supplier)
                .AsNoTracking().AsQueryable();

            if (request.SupplierId.HasValue) query = query.Where(c => c.SupplierId == request.SupplierId);
            if (request.StartDate.HasValue) query = query.Where(c => c.BillDate >= request.StartDate.Value);
            if (request.EndDate.HasValue) query = query.Where(c => c.BillDate <= request.EndDate.Value);

            var list = await query.OrderByDescending(c => c.Id).Select(c => new DebitNoteGridDto
            {
                Id = c.Id,
                DebitNoteNumber = c.BillNumber,
                Date = c.BillDate,
                SupplierName = c.Supplier.Name,
                GrandTotal = c.GrandTotal
            }).ToListAsync(cancellationToken);

            return Result<List<DebitNoteGridDto>>.Success(list);
        }
    }

    // ==========================================
    // 2. DEEP FETCH FOR VIEW MODAL
    // ==========================================
    public class GetDebitNoteByIdQuery : IRequest<Result<object>> { public int Id { get; set; } }

    public class GetDebitNoteByIdHandler : IRequestHandler<GetDebitNoteByIdQuery, Result<object>>
    {
        private readonly IErpDbContext _context;
        public GetDebitNoteByIdHandler(IErpDbContext context) => _context = context;

        public async Task<Result<object>> Handle(GetDebitNoteByIdQuery request, CancellationToken cancellationToken)
        {
            var dn = await _context.SupplierBills
                .Include(c => c.Supplier)
                .Include(c => c.Items).ThenInclude(i => i.ProductVariant)
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            if (dn == null) return Result<object>.Failure("Debit Note not found.");

            var data = new
            {
                dn.Id,
                DebitNoteNumber = dn.BillNumber,
                Date = dn.BillDate,
                SupplierName = dn.Supplier.Name,
                Reason = dn.Remarks,
                dn.SubTotal,
                TotalTax = dn.TaxAmount,
                dn.GrandTotal,
                Items = dn.Items.Where(i => i.ProductVariant != null).Select(i => new
                {
                    Description = i.ProductVariant!.Name,
                    Sku = i.ProductVariant.SKU,
                    Qty = i.Quantity,
                    Price = i.UnitPrice,
                    Total = i.LineTotal
                }).ToList()
            };

            return Result<object>.Success(data);
        }
    }
}
