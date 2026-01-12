using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IStockService
    {
        // The "Move" Function (Main -> Factory)
        Task<Result> TransferStockAsync(int productVariantId, int sourceWarehouseId, int targetWarehouseId, decimal qty, string referenceDoc);

        // The "Consume" Function (Factory -> Finished Good)
        Task<Result<decimal>> ConsumeStockAsync(int productVariantId, int warehouseId, decimal qty, string referenceDoc);

        // The "Receive" Function (Supplier -> Main)
        Task<Result> ReceiveStockAsync(int productVariantId, int warehouseId, decimal qty, decimal unitCost, string referenceDoc);
    }
}
