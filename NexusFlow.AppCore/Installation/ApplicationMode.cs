namespace NexusFlow.AppCore.Installation
{
    public enum ApplicationMode
    {
        Uninitialized = 0,
        Installing = 1,
        Installed = 2,
        UpgradeRequired = 3,
        Faulted = 4
    }
}
