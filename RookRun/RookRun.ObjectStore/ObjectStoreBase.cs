namespace RookRun.ObjectStore;

/// <summary>
/// Base class for <see cref="IObjectStore"/> implementations.
/// Provides default typed read/write operations built on top of
/// <see cref="StoreStreamAsync"/> and <see cref="TryReadStreamAsync"/>,
/// so concrete implementations only need to handle raw stream I/O.
/// </summary>
public abstract class ObjectStoreBase : IObjectStore
{
    private readonly ObjectStoreSerialization serialization;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStoreBase"/> class.
    /// </summary>
    /// <param name="serialization">The serialization helper used for typed read/write operations.</param>
    internal ObjectStoreBase(ObjectStoreSerialization serialization)
    {
        this.serialization = serialization ?? throw new ArgumentNullException(nameof(serialization));
    }

    /// <inheritdoc/>
    public abstract Task<IReadOnlyList<string>> ListObjectsAsync(string prefix, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task StoreStreamAsync(string path, Stream content, bool overwrite, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task<ObjectStoreObject<Stream>> TryReadStreamAsync(string path, DateTimeOffset? ifNewerThanUtc = null, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes <paramref name="obj"/> and writes it to the store via <see cref="StoreStreamAsync"/>.
    /// </summary>
    public async Task StoreObjectAsync<T>(string path, T obj, bool overwrite, CancellationToken cancellationToken = default)
    {
        await using var buffer = new MemoryStream();
        await serialization.SerializeAsync(buffer, obj, cancellationToken);
        buffer.Position = 0;
        await StoreStreamAsync(path, buffer, overwrite, cancellationToken);
    }

    /// <summary>
    /// Reads raw bytes via <see cref="TryReadStreamAsync"/> and deserializes them to <typeparamref name="T"/>.
    /// </summary>
    public async Task<ObjectStoreObject<T>> TryReadObjectAsync<T>(string path, DateTimeOffset? ifNewerThanUtc = null, CancellationToken cancellationToken = default)
    {
        var result = await TryReadStreamAsync(path, ifNewerThanUtc, cancellationToken);

        if (result.IsNotFound)
        {
            return ObjectStoreObject<T>.NotFound();
        }

        if (result.IsNotModified)
        {
            return ObjectStoreObject<T>.NotModified(result.LastModifiedUtc, result.ETag);
        }

        await using var stream = result.Value!;
        var value = await serialization.DeserializeAsync<T>(stream, cancellationToken);
        return ObjectStoreObject<T>.Found(value, result.LastModifiedUtc!.Value, result.ETag);
    }
}
