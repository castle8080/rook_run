using Microsoft.Extensions.Logging;
using RookRun.ObjectStore;

namespace RookRun.Job;

/// <summary>
/// Copies all objects from the configured <see cref="IObjectStore"/> to a local file system store under the <c>var</c> directory.
/// </summary>
public sealed class CopyObjectStoreJob : IJob
{
    private const string TargetDirectory = "var/object-store-copy";

    private readonly IObjectStore source;
    private readonly ILogger<CopyObjectStoreJob> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopyObjectStoreJob"/> class.
    /// </summary>
    /// <param name="source">The source object store resolved from DI.</param>
    /// <param name="logger">The logger used for job execution messages.</param>
    public CopyObjectStoreJob(IObjectStore source, ILogger<CopyObjectStoreJob> logger)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Copies all objects from the source store into a <see cref="FileSystemObjectStore"/> rooted at <c>var/object-store-copy</c>.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var targetDirectory = Path.GetFullPath(TargetDirectory);

        this.logger.LogInformation("Starting object store copy to '{TargetDirectory}'.", targetDirectory);

        var target = new FileSystemObjectStore(targetDirectory);
        var copier = new ObjectStoreCopier(source, target);

        var copied = await copier.CopyAsync(cancellationToken);

        this.logger.LogInformation("Object store copy complete. Copied {Count} object(s) to '{TargetDirectory}'.", copied, targetDirectory);
    }
}
