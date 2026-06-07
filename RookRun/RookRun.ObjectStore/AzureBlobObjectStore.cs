using Azure;
using Azure.Storage.Blobs;

namespace RookRun.ObjectStore;

public sealed class AzureBlobObjectStore : IObjectStore
{
    private readonly BlobContainerClient containerClient;
    private readonly ObjectStoreSerialization serialization;
    private readonly string rootPrefix;

    public AzureBlobObjectStore(BlobContainerClient containerClient, string? rootPrefix = null)
        : this(containerClient, new ObjectStoreJsonOptions(), rootPrefix)
    {
    }

    public AzureBlobObjectStore(BlobContainerClient containerClient, ObjectStoreJsonOptions options, string? rootPrefix = null)
    {
        this.containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        serialization = new ObjectStoreSerialization(options);
        this.rootPrefix = NormalizeRootPrefix(rootPrefix);
    }

    public async Task<IReadOnlyList<string>> ListObjectsAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var normalizedPrefix = ObjectStorePath.NormalizePrefix(prefix);
        var blobPrefix = rootPrefix + normalizedPrefix;
        var results = new List<string>();

        await foreach (var blob in containerClient.GetBlobsAsync(
            traits: Azure.Storage.Blobs.Models.BlobTraits.None,
            states: Azure.Storage.Blobs.Models.BlobStates.None,
            prefix: blobPrefix,
            cancellationToken: cancellationToken))
        {
            results.Add(blob.Name[rootPrefix.Length..]);
        }

        results.Sort(StringComparer.Ordinal);
        return results;
    }

    public async Task StoreObjectAsync<T>(string path, T obj, bool overwrite, CancellationToken cancellationToken = default)
    {
        var blobClient = containerClient.GetBlobClient(GetBlobName(path));
        await using var stream = await blobClient.OpenWriteAsync(overwrite: overwrite, cancellationToken: cancellationToken);
        await serialization.SerializeAsync(stream, obj, cancellationToken);
    }

    public async Task<T?> TryReadObjectAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        var blobClient = containerClient.GetBlobClient(GetBlobName(path));

        try
        {
            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            await using var content = response.Value.Content;
            return await serialization.DeserializeAsync<T>(content, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return default;
        }
    }

    public async Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default)
    {
        var blobClient = containerClient.GetBlobClient(GetBlobName(path));
        var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return response.Value;
    }

    internal BlobContainerClient ContainerClient => containerClient;

    internal string GetBlobName(string path) => rootPrefix + ObjectStorePath.NormalizeRequiredPath(path);

    private static string NormalizeRootPrefix(string? rootPrefix)
    {
        var normalizedPrefix = ObjectStorePath.NormalizePrefix(rootPrefix ?? string.Empty);
        return normalizedPrefix.Length == 0 ? string.Empty : $"{normalizedPrefix}/";
    }
}