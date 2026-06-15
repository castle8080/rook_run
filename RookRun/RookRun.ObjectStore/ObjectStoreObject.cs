namespace RookRun.ObjectStore;

/// <summary>
/// Represents the outcome state of an object store read operation.
/// </summary>
public enum ObjectStoreReadState
{
    /// <summary>
    /// The object exists and satisfies the read conditions.
    /// </summary>
    Found,

    /// <summary>
    /// No object exists at the requested path.
    /// </summary>
    NotFound,

    /// <summary>
    /// The object exists but was not modified after the requested cutoff.
    /// </summary>
    NotModified
}

/// <summary>
/// Represents an object loaded from the object store along with metadata that can be used for caching.
/// </summary>
/// <typeparam name="T">The value type stored in the object store.</typeparam>
public sealed class ObjectStoreObject<T>
{
    /// <summary>
    /// Gets the read outcome state.
    /// </summary>
    public required ObjectStoreReadState State { get; init; }

    /// <summary>
    /// Gets the deserialized object value when <see cref="State"/> is <see cref="ObjectStoreReadState.Found"/>.
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// Gets the UTC timestamp indicating when the stored object was last modified, when available.
    /// </summary>
    public DateTimeOffset? LastModifiedUtc { get; init; }

    /// <summary>
    /// Gets an optional backend-specific ETag for cache validation.
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// Gets a value indicating whether the read operation returned an object.
    /// </summary>
    public bool IsFound => State == ObjectStoreReadState.Found;

    /// <summary>
    /// Gets a value indicating whether no object exists at the requested path.
    /// </summary>
    public bool IsNotFound => State == ObjectStoreReadState.NotFound;

    /// <summary>
    /// Gets a value indicating whether the object exists but was not modified after the requested cutoff.
    /// </summary>
    public bool IsNotModified => State == ObjectStoreReadState.NotModified;

    /// <summary>
    /// Creates a read result representing a found object.
    /// </summary>
    /// <param name="value">The object value.</param>
    /// <param name="lastModifiedUtc">The object's last-modified timestamp in UTC.</param>
    /// <param name="eTag">An optional ETag.</param>
    /// <returns>A read result in the <see cref="ObjectStoreReadState.Found"/> state.</returns>
    public static ObjectStoreObject<T> Found(T? value, DateTimeOffset lastModifiedUtc, string? eTag = null) => new()
    {
        State = ObjectStoreReadState.Found,
        Value = value,
        LastModifiedUtc = lastModifiedUtc,
        ETag = eTag
    };

    /// <summary>
    /// Creates a read result representing a missing object.
    /// </summary>
    /// <returns>A read result in the <see cref="ObjectStoreReadState.NotFound"/> state.</returns>
    public static ObjectStoreObject<T> NotFound() => new()
    {
        State = ObjectStoreReadState.NotFound
    };

    /// <summary>
    /// Creates a read result representing an unchanged object for conditional reads.
    /// </summary>
    /// <param name="lastModifiedUtc">The known last-modified timestamp when available.</param>
    /// <param name="eTag">An optional ETag.</param>
    /// <returns>A read result in the <see cref="ObjectStoreReadState.NotModified"/> state.</returns>
    public static ObjectStoreObject<T> NotModified(DateTimeOffset? lastModifiedUtc = null, string? eTag = null) => new()
    {
        State = ObjectStoreReadState.NotModified,
        LastModifiedUtc = lastModifiedUtc,
        ETag = eTag
    };
}