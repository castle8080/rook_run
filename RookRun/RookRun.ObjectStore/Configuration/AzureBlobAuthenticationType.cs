namespace RookRun.ObjectStore.Configuration;

/// <summary>
/// Defines the authentication method used to connect to Azure Blob Storage.
/// </summary>
public enum AzureBlobAuthenticationType
{
    /// <summary>
    /// Uses <see cref="Azure.Identity.DefaultAzureCredential"/>.
    /// </summary>
    DefaultAzureCredential = 0,

    /// <summary>
    /// Uses an Azure Storage shared key credential.
    /// </summary>
    SharedKey = 1
}