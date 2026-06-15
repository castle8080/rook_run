using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RookRun.ObjectStore;

/// <summary>
/// An Azure Blob Storage-backed implementation of <see cref="IObjectStore"/>.
/// </summary>
public sealed class AzureBlobObjectStore : ObjectStoreBase
{
    private readonly BlobContainerClient containerClient;
    private readonly string rootPrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobObjectStore"/> class with default JSON options.
    /// </summary>
    /// <param name="containerClient">The Azure blob container client.</param>
    /// <param name="rootPrefix">An optional path prefix applied to all object paths.</param>
    public AzureBlobObjectStore(BlobContainerClient containerClient, string? rootPrefix = null)
        : this(containerClient, new ObjectStoreJsonOptions(), rootPrefix)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobObjectStore"/> class.
    /// </summary>
    /// <param name="containerClient">The Azure blob container client.</param>
    /// <param name="options">JSON serialization options.</param>
    /// <param name="rootPrefix">An optional path prefix applied to all object paths.</param>
    public AzureBlobObjectStore(BlobContainerClient containerClient, ObjectStoreJsonOptions options, string? rootPrefix = null)
        : base(new ObjectStoreSerialization(options))
    {
        this.containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        this.rootPrefix = NormalizeRootPrefix(rootPrefix);
    }

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<string>> ListObjectsAsync(string prefix, CancellationToken cancellationToken = default)
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

    /// <inheritdoc/>
    public override async Task StoreStreamAsync(string path, Stream content, bool overwrite, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var blobClient = containerClient.GetBlobClient(GetBlobName(path));
        await using var blobStream = await blobClient.OpenWriteAsync(overwrite: overwrite, cancellationToken: cancellationToken);
        await content.CopyToAsync(blobStream, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<ObjectStoreObject<Stream>> TryReadStreamAsync(string path, DateTimeOffset? ifNewerThanUtc = null, CancellationToken cancellationToken = default)
    {
        var blobClient = containerClient.GetBlobClient(GetBlobName(path));
        var downloadOptions = new BlobDownloadOptions();

        if (ifNewerThanUtc.HasValue)
        {
            downloadOptions.Conditions = new BlobRequestConditions
            {
                IfModifiedSince = ifNewerThanUtc.Value
            };
        }

        try
        {
            var response = await blobClient.DownloadStreamingAsync(downloadOptions, cancellationToken);

            // Buffer into memory so the network connection is not held open by the caller.
            await using var content = response.Value.Content;
            var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;

            return ObjectStoreObject<Stream>.Found(buffer, response.Value.Details.LastModified, response.Value.Details.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 304)
        {
            return ObjectStoreObject<Stream>.NotModified();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return ObjectStoreObject<Stream>.NotFound();
        }
    }

    /// <inheritdoc/>
    public override async Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default)
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