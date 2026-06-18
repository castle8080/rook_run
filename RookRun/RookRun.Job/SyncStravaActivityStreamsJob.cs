using Microsoft.Extensions.Logging;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.Job;

/// <summary>
/// Runs the Strava activity streams synchronization workflow.
/// Fetches and caches streams for recently synced activities.
/// </summary>
public sealed class SyncStravaActivityStreamsJob : IJob
{
    private const int LookBackDays = 365 * 5;

    private readonly ILogger<SyncStravaActivityStreamsJob> _logger;
    private readonly IStravaActivitiesRepository _activitiesRepository;
    private readonly SyncStravaActivityStreamsSynchronizer _synchronizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStravaActivityStreamsJob"/> class.
    /// </summary>
    /// <param name="logger">Logger for job execution messages.</param>
    /// <param name="activitiesRepository">Repository for retrieving activities to sync streams for.</param>
    /// <param name="synchronizer">The synchronizer that fetches and saves activity streams.</param>
    public SyncStravaActivityStreamsJob(
        ILogger<SyncStravaActivityStreamsJob> logger,
        IStravaActivitiesRepository activitiesRepository,
        SyncStravaActivityStreamsSynchronizer synchronizer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activitiesRepository = activitiesRepository ?? throw new ArgumentNullException(nameof(activitiesRepository));
        _synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
    }

    /// <summary>
    /// Executes the Strava activity streams synchronization job.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Strava activity streams sync job");

        try
        {
            var endDate = DateTimeOffset.UtcNow;
            var startDate = endDate.AddDays(-LookBackDays);
            var activityIds = await _activitiesRepository.ListActivityIdsAsync(startDate, endDate, cancellationToken);

            if (activityIds.Count == 0)
            {
                _logger.LogInformation("No activities found while syncing streams in range {StartDate} to {EndDate}", startDate, endDate);
                return;
            }

            _logger.LogInformation("Found {ActivityCount} source activities for stream sync", activityIds.Count);
            var totalSynced = await _synchronizer.SyncAsync(activityIds, cancellationToken);

            _logger.LogInformation("Completed Strava activity streams sync job. Synced {SyncedCount} activity stream documents", totalSynced);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Strava activity streams sync job cancelled. TokenCancellationRequested={TokenCancellationRequested}",
                cancellationToken.IsCancellationRequested);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Strava activity streams sync job");
            throw;
        }
    }
}
