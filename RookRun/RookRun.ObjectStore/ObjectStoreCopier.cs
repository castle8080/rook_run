namespace RookRun.ObjectStore;

/// <summary>
/// Copies all objects from one <see cref="IObjectStore"/> to another.
/// </summary>
public sealed class ObjectStoreCopier
{
    private readonly IObjectStore source;
    private readonly IObjectStore target;
    private readonly int maxParallelism;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStoreCopier"/> class.
    /// </summary>
    /// <param name="source">The object store to read objects from.</param>
    /// <param name="target">The object store to write objects to.</param>
    /// <param name="maxParallelism">The maximum number of concurrent copy operations. Must be at least 1.</param>
    public ObjectStoreCopier(IObjectStore source, IObjectStore target, int maxParallelism = 4)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (maxParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParallelism), maxParallelism, "Max parallelism must be at least 1.");
        }

        this.source = source;
        this.target = target;
        this.maxParallelism = maxParallelism;
    }

    /// <summary>
    /// Lists all objects in the source store and copies each one to the target store, overwriting any existing objects.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of objects copied.</returns>
    public async Task<int> CopyAsync(CancellationToken cancellationToken = default)
    {
        var paths = await source.ListObjectsAsync(string.Empty, cancellationToken);

        if (paths.Count == 0)
        {
            return 0;
        }

        var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
        var tasks = new List<Task>(paths.Count);

        foreach (var path in paths)
        {
            await semaphore.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var result = await source.TryReadStreamAsync(path, cancellationToken: cancellationToken);
                    if (result.IsFound)
                    {
                        await using var stream = result.Value!;
                        await target.StoreStreamAsync(path, stream, overwrite: true, cancellationToken);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        return paths.Count;
    }
}
