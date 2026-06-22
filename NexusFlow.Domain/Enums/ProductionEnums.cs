namespace NexusFlow.Domain.Enums
{
    public enum ProductionOrderStatus
    {
        Draft = 0,
        Released = 1,
        InProgress = 2,
        ReadyToClose = 3,
        Closed = 4,
        Cancelled = 5
    }

    public enum ProductionMaterialMovementType
    {
        Issue = 1,
        Return = 2
    }

    public enum ProductionClaimStatus
    {
        Open = 0,
        Settled = 1,
        Cancelled = 2
    }
}
