namespace RookRun.ObjectStore;

/// <summary>
/// A file-system-backed implementation of <see cref="IObjectStore"/>.
/// </summary>
public sealed class FileSystemObjectStore : ObjectStoreBase
{
    private readonly string rootDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemObjectStore"/> class with default JSON options.
    /// </summary>
    /// <param name="rootDirectory">The root directory in which objects are stored.</param>
    public FileSystemObjectStore(string rootDirectory)
        : this(rootDirectory, new ObjectStoreJsonOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemObjectStore"/> class.
    /// </summary>
    /// <param name="rootDirectory">The root directory in which objects are stored.</param>
    /// <param name="options">JSON serialization options.</param>
    public FileSystemObjectStore(string rootDirectory, ObjectStoreJsonOptions options)
        : base(new ObjectStoreSerialization(options))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        this.rootDirectory = Path.GetFullPath(rootDirectory);
    }

    /// <inheritdoc/>
    public override Task<IReadOnlyList<string>> ListObjectsAsync(string prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPrefix = ObjectStorePath.NormalizePrefix(prefix);
        if (!Directory.Exists(rootDirectory))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var results = Directory
            .EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories)
            .Select(ToObjectPath)
            .Where(path => normalizedPrefix.Length == 0 || path.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(results);
    }

    /// <inheritdoc/>
    public override async Task StoreStreamAsync(string path, Stream content, bool overwrite, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;

        await using var stream = new FileStream(fullPath, fileMode, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(stream, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<ObjectStoreObject<Stream>> TryReadStreamAsync(string path, DateTimeOffset? ifNewerThanUtc = null, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return ObjectStoreObject<Stream>.NotFound();
        }

        var lastModifiedUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(fullPath));
        if (ifNewerThanUtc.HasValue && lastModifiedUtc <= ifNewerThanUtc.Value)
        {
            return ObjectStoreObject<Stream>.NotModified(lastModifiedUtc);
        }

        // Buffer into memory so the file handle is not held open by the caller.
        await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return ObjectStoreObject<Stream>.Found(buffer, lastModifiedUtc);
    }

    /// <inheritdoc/>
    public override Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    private string GetFullPath(string objectPath)
    {
        var normalizedPath = ObjectStorePath.NormalizeRequiredPath(objectPath);
        var relativePath = normalizedPath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));

        if (!fullPath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Object path '{objectPath}' resolves outside the configured root directory.");
        }

        return fullPath;
    }

    private string ToObjectPath(string filePath)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, filePath);
        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }
}