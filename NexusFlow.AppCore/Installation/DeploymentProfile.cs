namespace NexusFlow.AppCore.Installation
{
    public enum DeploymentProfile
    {
        WindowsIis = 0,
        PortableVm = 1,
        AzureAppService = 2
    }

    public enum InstallationStateStoreMode
    {
        File = 0,
        AzureBlob = 1
    }

    public enum InstallationSecretStoreMode
    {
        Dpapi = 0,
        EncryptedFile = 1,
        Environment = 2
    }

    public enum DataProtectionStoreMode
    {
        File = 0,
        AzureBlob = 1
    }

    public enum StorageMode
    {
        Local = 0,
        AzureBlob = 1,
        Hybrid = 2
    }
}
