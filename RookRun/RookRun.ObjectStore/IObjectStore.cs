using RookRun.Common.Exceptions;

namespace RookRun.ObjectStore;

public interface IObjectStore
{
    Task<IReadOnlyList<string>> ListObjectsAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a raw pre-serialized stream to the specified path.
    /// The caller is responsible for ensuring the stream content matches the format expected by the store.
    /// </summary>
    /// <param name="path">The path at which to store the stream.</param>
    /// <param name="content">The stream content to store.</param>
    /// <param name="overwrite">If false, fails if object already exists. If true, overwrites or creates.</param>
    /// <param name="ifMatchETag">Optional ETag for optimistic concurrency. If supplied, update only succeeds if current ETag matches. If object is missing, throws <see cref="ObjectStorePreconditionFailedException"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when overwrite=false and ifMatchETag is both supplied (conflicting semantics).</exception>
    /// <exception cref="ObjectStorePreconditionFailedException">Thrown when object exists but ETag does not match, or when object is missing and ifMatchETag is supplied.</exception>
    Task StoreStreamAsync(string path, Stream content, bool overwrite, string? ifMatchETag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the raw stored bytes for the specified path as a stream.
    /// When <see cref="ObjectStoreReadState.Found"/>, the caller owns and must dispose the stream.
    /// </summary>
    Task<ObjectStoreObject<Stream>> TryReadStreamAsync(string path, DateTimeOffset? ifNewerThanUtc = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an object at the specified path, serialized using the configured JSON options.
    /// </summary>
    /// <param name="path">The path at which to store the object.</param>
    /// <param name="obj">The object to serialize and store.</param>
    /// <param name="overwrite">If false, fails if object already exists. If true, overwrites or creates.</param>
    /// <param name="ifMatchETag">Optional ETag for optimistic concurrency. If supplied, update only succeeds if current ETag matches. If object is missing, throws <see cref="ObjectStorePreconditionFailedException"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when overwrite=false and ifMatchETag is both supplied (conflicting semantics).</exception>
    /// <exception cref="ObjectStorePreconditionFailedException">Thrown when object exists but ETag does not match, or when object is missing and ifMatchETag is supplied.</exception>
    Task StoreObjectAsync<T>(string path, T obj, bool overwrite, string? ifMatchETag = null, CancellationToken cancellationToken = default);

    Task<ObjectStoreObject<T>> TryReadObjectAsync<T>(string path, DateTimeOffset? ifNewerThanUtc = null, CancellationToken cancellationToken = default);

    Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default);
}