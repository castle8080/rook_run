using Microsoft.Extensions.Logging;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.Job;

/// <summary>
/// Runs the Strava activity image synchronization workflow.
/// Downloads and caches images from activities with cached details.
/// </summary>
public sealed class SyncStravaActivityImageJob : IJob
{
    private const int LookBackDays = 365 * 5;

    private readonly ILogger<SyncStravaActivityImageJob> _logger;
    private readonly IStravaActivitiesRepository _activitiesRepository;
    private readonly SyncStravaActivityImageSynchronizer _synchronizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStravaActivityImageJob"/> class.
    /// </summary>
    /// <param name="logger">Logger for job execution messages.</param>
    /// <param name="activitiesRepository">Repository for retrieving activities to sync images for.</param>
    /// <param name="synchronizer">The synchronizer that downloads and saves activity images.</param>
    public SyncStravaActivityImageJob(
        ILogger<SyncStravaActivityImageJob> logger,
        IStravaActivitiesRepository activitiesRepository,
        SyncStravaActivityImageSynchronizer synchronizer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activitiesRepository = activitiesRepository ?? throw new ArgumentNullException(nameof(activitiesRepository));
        _synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
    }

    /// <summary>
    /// Executes the Strava activity image synchronization job.
    /// Retrieves recently added activities and downloads their images.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Strava activity image sync job");

        try
        {
            var endDate = DateTimeOffset.UtcNow;
            var startDate = endDate.AddDays(-LookBackDays);
            var activityIds = await _activitiesRepository.ListActivityIdsAsync(startDate, endDate, cancellationToken);

            if (activityIds.Count == 0)
            {
                _logger.LogInformation("No activities found while syncing images in range {StartDate} to {EndDate}", startDate, endDate);
                return;
            }

            _logger.LogInformation("Found {ActivityCount} source activities for image sync", activityIds.Count);
            var totalSynced = await _synchronizer.SyncAsync(activityIds, cancellationToken);

            _logger.LogInformation("Completed Strava activity image sync job. Synced {ImageCount} images", totalSynced);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Strava activity image sync job cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Strava activity image sync job");
            throw;
        }
    }
}
