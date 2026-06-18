using RookRun.Common.Exceptions;

namespace RookRun.ObjectStore;

/// <summary>
/// A file-system-backed implementation of <see cref="IObjectStore"/>.
/// Uses the file's last-write timestamp as an ETag in the format mtime:TICKS where TICKS is FileInfo.LastWriteTimeUtc.Ticks.
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
    public override async Task StoreStreamAsync(string path, Stream content, bool overwrite, string? ifMatchETag = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        if (!overwrite && ifMatchETag != null)
        {
            throw new ArgumentException("Cannot specify both overwrite=false and ifMatchETag. These semantics are conflicting.", nameof(ifMatchETag));
        }

        var fullPath = GetFullPath(path);

        if (ifMatchETag != null)
        {
            // Optimistic concurrency: ETag must match current file state
            if (!File.Exists(fullPath))
            {
                throw new PreconditionFailedException($"Object at path '{path}' does not exist, but ETag condition was specified.");
            }

            var currentETag = GetFileETag(fullPath);
            if (currentETag != ifMatchETag)
            {
                throw new PreconditionFailedException($"ETag mismatch at path '{path}'. Expected '{ifMatchETag}', but found '{currentETag}'.");
            }
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;

        try
        {
            await using var stream = new FileStream(fullPath, fileMode, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(stream, cancellationToken);
        }
        catch (IOException ex) when (!overwrite && File.Exists(fullPath))
        {
            // CreateNew failed because file exists
            throw new IOException($"An object already exists at path '{path}'.", ex);
        }
    }

    /// <inheritdoc/>
    public override async Task<ObjectStoreObject<Stream>> TryReadStreamAsync(string path, DateTimeOffset? ifNewerThanUtc = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return ObjectStoreObject<Stream>.NotFound();
        }

        var lastModifiedUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(fullPath));
        if (ifNewerThanUtc.HasValue && lastModifiedUtc <= ifNewerThanUtc.Value)
        {
            var notModifiedETag = GetFileETag(fullPath);
            return ObjectStoreObject<Stream>.NotModified(lastModifiedUtc, notModifiedETag);
        }

        // Buffer into memory so the file handle is not held open by the caller.
        await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        var eTag = GetFileETag(fullPath);
        return ObjectStoreObject<Stream>.Found(buffer, lastModifiedUtc, eTag);
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

    /// <summary>
    /// Generates an ETag for a file based on its last-write timestamp.
    /// Format: mtime:TICKS where TICKS is the UTC last-write time in 100-nanosecond intervals.
    /// </summary>
    private static string GetFileETag(string fullPath)
    {
        var lastWriteUtc = File.GetLastWriteTimeUtc(fullPath);
        var ticks = new DateTimeOffset(lastWriteUtc).Ticks;
        return FormattableString.Invariant($"mtime:{ticks}");
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