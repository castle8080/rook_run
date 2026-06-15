using System.Collections.Concurrent;

namespace RookRun.ObjectStore;

/// <summary>
/// An in-memory implementation of <see cref="IObjectStore"/> backed by a concurrent dictionary.
/// </summary>
public sealed class InMemoryObjectStore : ObjectStoreBase
{
    private readonly ConcurrentDictionary<string, StoredObject> objects = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryObjectStore"/> class with default JSON options.
    /// </summary>
    public InMemoryObjectStore()
        : this(new ObjectStoreJsonOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryObjectStore"/> class.
    /// </summary>
    /// <param name="options">JSON serialization options.</param>
    public InMemoryObjectStore(ObjectStoreJsonOptions options)
        : base(new ObjectStoreSerialization(options))
    {
    }

    /// <inheritdoc/>
    public override Task<IReadOnlyList<string>> ListObjectsAsync(string prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPrefix = ObjectStorePath.NormalizePrefix(prefix);
        var matches = objects.Keys
            .Where(key => normalizedPrefix.Length == 0 || key.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(matches);
    }

    /// <inheritdoc/>
    public override async Task StoreStreamAsync(string path, Stream content, bool overwrite, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = ObjectStorePath.NormalizeRequiredPath(path);

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var storedObject = new StoredObject(buffer.ToArray(), DateTimeOffset.UtcNow);

        if (overwrite)
        {
            objects[normalizedPath] = storedObject;
            return;
        }

        if (!objects.TryAdd(normalizedPath, storedObject))
        {
            throw new IOException($"An object already exists at path '{normalizedPath}'.");
        }
    }

    /// <inheritdoc/>
    public override Task<ObjectStoreObject<Stream>> TryReadStreamAsync(string path, DateTimeOffset? ifNewerThanUtc = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = ObjectStorePath.NormalizeRequiredPath(path);
        if (!objects.TryGetValue(normalizedPath, out var storedObject))
        {
            return Task.FromResult(ObjectStoreObject<Stream>.NotFound());
        }

        if (ifNewerThanUtc.HasValue && storedObject.LastModifiedUtc <= ifNewerThanUtc.Value)
        {
            return Task.FromResult(ObjectStoreObject<Stream>.NotModified(storedObject.LastModifiedUtc));
        }

        Stream stream = new MemoryStream(storedObject.Payload, writable: false);
        return Task.FromResult(ObjectStoreObject<Stream>.Found(stream, storedObject.LastModifiedUtc));
    }

    /// <inheritdoc/>
    public override Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = ObjectStorePath.NormalizeRequiredPath(path);
        return Task.FromResult(objects.TryRemove(normalizedPath, out _));
    }

    private sealed record StoredObject(byte[] Payload, DateTimeOffset LastModifiedUtc);
}