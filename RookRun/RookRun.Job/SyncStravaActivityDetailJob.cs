using Microsoft.Extensions.Logging;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.Job;

/// <summary>
/// Runs the Strava activity detail synchronization workflow.
/// Fetches detailed activity information for recently synced activities.
/// </summary>
public sealed class SyncStravaActivityDetailJob : IJob
{
    private const int LookBackDays = 365 * 5;

    private readonly ILogger<SyncStravaActivityDetailJob> _logger;
    private readonly IStravaActivitiesRepository _activitiesRepository;
    private readonly SyncStravaActivityDetailSynchronizer _synchronizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStravaActivityDetailJob"/> class.
    /// </summary>
    /// <param name="logger">Logger for job execution messages.</param>
    /// <param name="activitiesRepository">Repository for retrieving activities to sync details for.</param>
    /// <param name="synchronizer">The synchronizer that fetches and saves activity details.</param>
    public SyncStravaActivityDetailJob(
        ILogger<SyncStravaActivityDetailJob> logger,
        IStravaActivitiesRepository activitiesRepository,
        SyncStravaActivityDetailSynchronizer synchronizer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activitiesRepository = activitiesRepository ?? throw new ArgumentNullException(nameof(activitiesRepository));
        _synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
    }

    /// <summary>
    /// Executes the Strava activity detail synchronization job.
    /// Retrieves recently added activities and fetches their detailed information.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Strava activity detail sync job");

        try
        {
            var endDate = DateTimeOffset.UtcNow;
            var startDate = endDate.AddDays(-LookBackDays);
            var activityIds = await _activitiesRepository.ListActivityIdsAsync(startDate, endDate, cancellationToken);

            if (activityIds.Count == 0)
            {
                _logger.LogInformation("No activities found while syncing details in range {StartDate} to {EndDate}", startDate, endDate);
                return;
            }

            _logger.LogInformation("Found {ActivityCount} source activities for detail sync", activityIds.Count);

            var totalSynced = await _synchronizer.SyncAsync(activityIds, cancellationToken);

            _logger.LogInformation("Completed Strava activity detail sync job. Synced {SyncedCount} activity details", totalSynced);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Strava activity detail sync job cancelled. TokenCancellationRequested={TokenCancellationRequested}",
                cancellationToken.IsCancellationRequested);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Strava activity detail sync job");
            throw;
        }
    }
}
