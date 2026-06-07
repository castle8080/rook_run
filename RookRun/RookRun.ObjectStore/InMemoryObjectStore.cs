using System.Collections.Concurrent;

namespace RookRun.ObjectStore;

public sealed class InMemoryObjectStore : IObjectStore
{
    private readonly ConcurrentDictionary<string, byte[]> objects = new(StringComparer.Ordinal);
    private readonly ObjectStoreSerialization serialization;

    public InMemoryObjectStore()
        : this(new ObjectStoreJsonOptions())
    {
    }

    public InMemoryObjectStore(ObjectStoreJsonOptions options)
    {
        serialization = new ObjectStoreSerialization(options);
    }

    public Task<IReadOnlyList<string>> ListObjectsAsync(string prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPrefix = ObjectStorePath.NormalizePrefix(prefix);
        var matches = objects.Keys
            .Where(key => normalizedPrefix.Length == 0 || key.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(matches);
    }

    public async Task StoreObjectAsync<T>(string path, T obj, bool overwrite, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = ObjectStorePath.NormalizeRequiredPath(path);
        var payload = await serialization.SerializeToMemoryAsync(obj, cancellationToken);

        if (overwrite)
        {
            objects[normalizedPath] = payload;
            return;
        }

        if (!objects.TryAdd(normalizedPath, payload))
        {
            throw new IOException($"An object already exists at path '{normalizedPath}'.");
        }
    }

    public async Task<T?> TryReadObjectAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = ObjectStorePath.NormalizeRequiredPath(path);
        if (!objects.TryGetValue(normalizedPath, out var payload))
        {
            return default;
        }

        return await serialization.DeserializeAsync<T>(payload, cancellationToken);
    }

    public Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = ObjectStorePath.NormalizeRequiredPath(path);
        return Task.FromResult(objects.TryRemove(normalizedPath, out _));
    }
}