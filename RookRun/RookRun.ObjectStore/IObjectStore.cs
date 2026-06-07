namespace RookRun.ObjectStore;

public interface IObjectStore
{
    Task<IReadOnlyList<string>> ListObjectsAsync(string prefix, CancellationToken cancellationToken = default);

    Task StoreObjectAsync<T>(string path, T obj, bool overwrite, CancellationToken cancellationToken = default);

    Task<T?> TryReadObjectAsync<T>(string path, CancellationToken cancellationToken = default);

    Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default);
}