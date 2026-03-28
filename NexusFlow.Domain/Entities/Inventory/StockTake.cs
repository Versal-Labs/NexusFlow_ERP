using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NexusFlow.Domain.Entities.Inventory
{
    [Table("StockTake", Schema = "Inventory")]
    public class StockTake : AuditableEntity
    {
        public int Id { get; set; }
        public string StockTakeNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; }

        public StockTakeStatus Status { get; set; } = StockTakeStatus.Initiated;

        public string Notes { get; set; } = string.Empty;

        // The net financial impact (Negative = Shrinkage Loss, Positive = Surplus Gain)
        public decimal TotalVarianceValue { get; set; }

        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public ICollection<StockTakeItem> Items { get; set; } = new List<StockTakeItem>();
    }
}
