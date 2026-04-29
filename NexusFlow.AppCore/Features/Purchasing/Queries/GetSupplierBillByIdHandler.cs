using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Queries
{
    public class GetSupplierBillByIdQuery : IRequest<Result<SupplierBillDetailsDto>> { public int Id { get; set; } }

    public class SupplierBillDetailsDto : SupplierBillDto
    {
        public string Remarks { get; set; } = string.Empty;
        public bool ApplyVat { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public List<SupplierBillItemDto> Items { get; set; } = new();
        public List<int> LinkedGrnIds { get; set; } = new();
    }

    public class SupplierBillItemDto
    {
        public int? ProductVariantId { get; set; }
        public int? ExpenseAccountId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class GetSupplierBillByIdHandler : IRequestHandler<GetSupplierBillByIdQuery, Result<SupplierBillDetailsDto>>
    {
        private readonly IErpDbContext _context;
        public GetSupplierBillByIdHandler(IErpDbContext context) => _context = context;

        public async Task<Result<SupplierBillDetailsDto>> Handle(GetSupplierBillByIdQuery request, CancellationToken cancellationToken)
        {
            var bill = await _context.SupplierBills
                .Include(b => b.Supplier)
                .Include(b => b.Items)
                .Include(b => b.Grns)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

            if (bill == null) return Result<SupplierBillDetailsDto>.Failure("Supplier Bill not found.");

            var dto = new SupplierBillDetailsDto
            {
                Id = bill.Id,
                BillNumber = bill.BillNumber,
                SupplierInvoiceNo = bill.SupplierInvoiceNo,
                BillDate = bill.BillDate,
                DueDate = bill.DueDate,
                SupplierName = bill.Supplier.Name,
                GrandTotal = bill.GrandTotal,
                AmountPaid = bill.AmountPaid,
                IsPosted = bill.IsPosted,
                PaymentStatus = bill.PaymentStatus.ToString(),
                Remarks = bill.Remarks,
                ApplyVat = bill.ApplyVat,
                SubTotal = bill.SubTotal,
                TaxAmount = bill.TaxAmount,
                LinkedGrnIds = bill.Grns.Select(g => g.Id).ToList(),
                Items = bill.Items.Select(i => new SupplierBillItemDto
                {
                    ProductVariantId = i.ProductVariantId,
                    ExpenseAccountId = i.ExpenseAccountId,
                    Description = i.Description,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.LineTotal
                }).ToList()
            };

            return Result<SupplierBillDetailsDto>.Success(dto);
        }
    }
}
