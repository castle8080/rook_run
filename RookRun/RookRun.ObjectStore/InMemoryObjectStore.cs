using System.Collections.Concurrent;
using RookRun.Common.Exceptions;

namespace RookRun.ObjectStore;

/// <summary>
/// An in-memory implementation of <see cref="IObjectStore"/> backed by a concurrent dictionary.
/// Uses opaque version tokens as ETags in the format v:N where N is incremented on each mutation.
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
    public override async Task StoreStreamAsync(string path, Stream content, bool overwrite, string? ifMatchETag = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        if (!overwrite && ifMatchETag != null)
        {
            throw new ArgumentException("Cannot specify both overwrite=false and ifMatchETag. These semantics are conflicting.", nameof(ifMatchETag));
        }

        var normalizedPath = ObjectStorePath.NormalizeRequiredPath(path);

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var payload = buffer.ToArray();
        var now = DateTimeOffset.UtcNow;

        if (ifMatchETag != null)
        {
            // Optimistic concurrency: ETag must match
            if (!objects.TryGetValue(normalizedPath, out var existing))
            {
                throw new ObjectStorePreconditionFailedException($"Object at path '{normalizedPath}' does not exist, but ETag condition was specified.");
            }

            if (existing.ETag != ifMatchETag)
            {
                throw new ObjectStorePreconditionFailedException($"ETag mismatch at path '{normalizedPath}'. Expected '{ifMatchETag}', but found '{existing.ETag}'.");
            }

            // Increment version on successful update
            var newVersion = existing.Version + 1;
            var updated = new StoredObject(payload, now, newVersion);
            objects[normalizedPath] = updated;
            return;
        }

        if (overwrite)
        {
            // Unconditional overwrite or create; increment version if exists, otherwise start at 1
            var newVersion = objects.TryGetValue(normalizedPath, out var existing) ? existing.Version + 1 : 1;
            objects[normalizedPath] = new StoredObject(payload, now, newVersion);
            return;
        }

        // overwrite=false, no ETag: only succeed if path does not exist
        if (!objects.TryAdd(normalizedPath, new StoredObject(payload, now, 1)))
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
            return Task.FromResult(ObjectStoreObject<Stream>.NotModified(storedObject.LastModifiedUtc, storedObject.ETag));
        }

        Stream stream = new MemoryStream(storedObject.Payload, writable: false);
        return Task.FromResult(ObjectStoreObject<Stream>.Found(stream, storedObject.LastModifiedUtc, storedObject.ETag));
    }

    /// <inheritdoc/>
    public override Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = ObjectStorePath.NormalizeRequiredPath(path);
        return Task.FromResult(objects.TryRemove(normalizedPath, out _));
    }

    private sealed record StoredObject(byte[] Payload, DateTimeOffset LastModifiedUtc, long Version)
    {
        /// <summary>
        /// Gets the ETag in the format v:N where N is the version number.
        /// </summary>
        public string ETag => FormattableString.Invariant($"v:{Version}");
    }
}