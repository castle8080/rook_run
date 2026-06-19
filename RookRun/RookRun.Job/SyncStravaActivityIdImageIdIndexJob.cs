using Microsoft.Extensions.Logging;
using RookRun.Strava.Sync;

namespace RookRun.Job;

/// <summary>
/// Runs the Strava activity image-id index synchronization workflow.
/// </summary>
public sealed class SyncStravaActivityIdImageIdIndexJob : IJob
{
    private readonly ILogger<SyncStravaActivityIdImageIdIndexJob> _logger;
    private readonly SyncStravaActivityIdImageIdIndexSynchronizer _synchronizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStravaActivityIdImageIdIndexJob"/> class.
    /// </summary>
    /// <param name="logger">Logger for job execution messages.</param>
    /// <param name="synchronizer">The synchronizer that builds the image-id index.</param>
    public SyncStravaActivityIdImageIdIndexJob(
        ILogger<SyncStravaActivityIdImageIdIndexJob> logger,
        SyncStravaActivityIdImageIdIndexSynchronizer synchronizer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
    }

    /// <summary>
    /// Executes the Strava activity image-id index synchronization job.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Strava activity image-id index sync job");

        try
        {
            var totalIndexed = await _synchronizer.SyncAsync(cancellationToken);
            _logger.LogInformation("Completed Strava activity image-id index sync job. Indexed {IndexedCount} activities", totalIndexed);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Strava activity image-id index sync job cancelled. TokenCancellationRequested={TokenCancellationRequested}",
                cancellationToken.IsCancellationRequested);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Strava activity image-id index sync job");
            throw;
        }
    }
}