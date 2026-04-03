using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IStockService
    {
        // The "Move" Function (Main -> Factory)
        Task<Result> TransferStockAsync(int productVariantId, int sourceWarehouseId, int targetWarehouseId, decimal qty, string referenceDoc, string notes = "");

        // The "Consume" Function (Factory -> Finished Good)
        Task<Result<decimal>> ConsumeStockAsync(int productVariantId, int warehouseId, decimal qty, string referenceDoc, string notes = "");

        // The "Receive" Function (Supplier -> Main)
        Task<Result<decimal>> ReceiveStockAsync(int productVariantId, int warehouseId, decimal qty, decimal unitCost, string referenceDoc, string notes = "");
        Task<Result<int>> RestoreStockAsync(int variantId, int warehouseId, decimal qty, decimal originalTotalCogs, string referenceNo, string reason);
        Task<decimal> IssueStockAsync(int productVariantId, int warehouseId, decimal qty, string referenceDoc, string notes = "");
    }
}
