namespace RookRun.ObjectStore;

public sealed class FileSystemObjectStore : IObjectStore
{
    private readonly string rootDirectory;
    private readonly ObjectStoreSerialization serialization;

    public FileSystemObjectStore(string rootDirectory)
        : this(rootDirectory, new ObjectStoreJsonOptions())
    {
    }

    public FileSystemObjectStore(string rootDirectory, ObjectStoreJsonOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        this.rootDirectory = Path.GetFullPath(rootDirectory);
        serialization = new ObjectStoreSerialization(options);
    }

    public Task<IReadOnlyList<string>> ListObjectsAsync(string prefix, CancellationToken cancellationToken = default)
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

    public async Task StoreObjectAsync<T>(string path, T obj, bool overwrite, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;

        await using var stream = new FileStream(fullPath, fileMode, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await serialization.SerializeAsync(stream, obj, cancellationToken);
    }

    public async Task<T?> TryReadObjectAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return default;
        }

        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return await serialization.DeserializeAsync<T>(stream, cancellationToken);
    }

    public Task<bool> TryDeleteObjectAsync(string path, CancellationToken cancellationToken = default)
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