namespace RookRun.ObjectStore;

public interface IObjectStore
{
    Task<IReadOnlyList<string>> ListObjectsAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a raw pre-serialized stream to the specified path.
    /// The caller is responsible for ensuring the stream content matches the format expected by the store.
    /// </summary>
    Task StoreStreamAsync(string path, Stream content, bool overwrite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the raw stored bytes for the specified path as a stream.
    /// When <see cref="ObjectStoreReadState.Found"/>, the caller owns and must dispose the stream.
    /// </summary>
    Task<ObjectStoreObject<Stream>> TryReadStreamAsync(string path, DateTimeOffset? ifNewerThanUtc = null, CancellationToken cancellationToken = default);

    Task StoreObjectAsync<T>(string path, T obj, bool overwrite, CancellationToken cancellationToken = default);

    Task<ObjectStoreObject<T>> TryReadObjectAsync<T>(string path, DateTimeOffset? ifNewerThanUtc = null, CancellationToken cancellationToken = default);

    Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default);
}