namespace RookRun.ObjectStore.Configuration;

public sealed class AzureBlobObjectStoreOptions
{
    public AzureBlobAuthenticationType Authentication { get; set; } = AzureBlobAuthenticationType.DefaultAzureCredential;

    public string ServiceUri { get; set; } = string.Empty;

    public string ContainerName { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public string AccountKey { get; set; } = string.Empty;

    public string RootPrefix { get; set; } = string.Empty;
}