using Microsoft.Extensions.Logging;
using RookRun.Strava.Sync;

namespace RookRun.Job;

/// <summary>
/// Runs the Strava activities synchronization workflow.
/// </summary>
public sealed class SyncStravaActivitiesJob : IJob
{
    private readonly ILogger<SyncStravaActivitiesJob> logger;
    private readonly StravaActivitiesSynchronizer synchronizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStravaActivitiesJob"/> class.
    /// </summary>
    /// <param name="logger">The logger used for job execution messages.</param>
    /// <param name="synchronizer">The synchronizer that fetches and saves Strava activities.</param>
    public SyncStravaActivitiesJob(
        ILogger<SyncStravaActivitiesJob> logger,
        StravaActivitiesSynchronizer synchronizer)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
    }

    /// <summary>
    /// Executes the Strava activities synchronization job.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Starting Strava activities sync job.");

        var syncedCount = await this.synchronizer.SyncAsync();

        this.logger.LogInformation("Completed Strava activities sync job. Synced {SyncedCount} new activities.", syncedCount);
    }
}
