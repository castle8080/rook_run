namespace RookRun.ObjectStore.Configuration;

public sealed class AzureBlobObjectStoreOptions
{
    public string ServiceUri { get; set; } = string.Empty;

    public string ContainerName { get; set; } = string.Empty;

    public string RootPrefix { get; set; } = string.Empty;
}