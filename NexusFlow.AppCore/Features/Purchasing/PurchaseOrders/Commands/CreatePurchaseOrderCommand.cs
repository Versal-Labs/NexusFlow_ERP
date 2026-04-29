using MediatR;
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
    public class CreatePurchaseOrderCommand : IRequest<Result<int>>
    {
        public int SupplierId { get; set; }
        public DateTime Date { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string Note { get; set; } = string.Empty;
        public bool IsDraft { get; set; } // TIER-1 FIX: Added IsDraft flag
        public List<PurchaseOrderItemDto> Items { get; set; } = new();
    }

    public class CreatePurchaseOrderHandler : IRequestHandler<CreatePurchaseOrderCommand, Result<int>>
    {
        private readonly IErpDbContext _context;
        private readonly INumberSequenceService _sequenceService;

        public CreatePurchaseOrderHandler(IErpDbContext context, INumberSequenceService sequenceService)
        {
            _context = context;
            _sequenceService = sequenceService;
        }

        public async Task<Result<int>> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
        {
            if (request.Items == null || !request.Items.Any())
                return Result<int>.Failure("Cannot create an empty Purchase Order.");

            string poNumber = await _sequenceService.GenerateNextNumberAsync("Purchasing", cancellationToken);

            var po = new PurchaseOrder
            {
                PoNumber = poNumber,
                Date = request.Date,
                ExpectedDate = request.ExpectedDate,
                SupplierId = request.SupplierId,

                // TIER-1 FIX: Respect the UI button clicked!
                Status = request.IsDraft ? PurchaseOrderStatus.Draft : PurchaseOrderStatus.Approved,

                Items = new List<PurchaseOrderItem>(),
                Note = request.Note
            };

            decimal grandTotal = 0;

            foreach (var item in request.Items)
            {
                if (item.QuantityOrdered <= 0) continue;

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

            _context.PurchaseOrders.Add(po);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(po.Id, $"Purchase Order {poNumber} created successfully.");
        }
    }
}
