using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Purchasing;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.PurchaseOrders.Commands
{
    public class UpdatePurchaseOrderCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public DateTime Date { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string Note { get; set; } = string.Empty;
        public bool IsDraft { get; set; }
        public List<PurchaseOrderItemDto> Items { get; set; } = new();
    }

    public class UpdatePurchaseOrderHandler : IRequestHandler<UpdatePurchaseOrderCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        public UpdatePurchaseOrderHandler(IErpDbContext context) => _context = context;

        public async Task<Result<int>> Handle(UpdatePurchaseOrderCommand request, CancellationToken cancellationToken)
        {
            var po = await _context.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (po == null) return Result<int>.Failure("Purchase Order not found.");

            // Tier-1 Validation: You cannot edit a PO once it has been approved/received
            if (po.Status != PurchaseOrderStatus.Draft)
                return Result<int>.Failure("Only Draft Purchase Orders can be edited.");

            po.Date = request.Date;
            po.ExpectedDate = request.ExpectedDate;
            po.SupplierId = request.SupplierId;
            po.Note = request.Note;

            // If the user clicked "Confirm & Approve", advance the status
            po.Status = request.IsDraft ? PurchaseOrderStatus.Draft : PurchaseOrderStatus.Approved;

            // Clear old items and rebuild (safest way to handle dynamic grid editing)
            _context.PurchaseOrderItems.RemoveRange(po.Items);

            decimal grandTotal = 0;
            foreach (var item in request.Items.Where(i => i.QuantityOrdered > 0))
            {
                po.Items.Add(new PurchaseOrderItem
                {
                    ProductVariantId = item.ProductVariantId,
                    QuantityOrdered = item.QuantityOrdered,
                    UnitCost = item.UnitCost,
                    QuantityReceived = 0
                });
                grandTotal += (item.QuantityOrdered * item.UnitCost);
            }

            po.TotalAmount = grandTotal;

            await _context.SaveChangesAsync(cancellationToken);
            return Result<int>.Success(po.Id, $"Purchase Order {po.PoNumber} updated successfully.");
        }
    }
}
