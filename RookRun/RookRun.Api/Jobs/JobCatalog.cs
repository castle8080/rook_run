using RookRun.Contracts.Jobs;
using RookRun.Job;

namespace RookRun.Api.Jobs;

/// <summary>
/// Provides metadata for known jobs that can be executed by the API.
/// </summary>
public static class JobCatalog
{
    /// <summary>
    /// Gets the known jobs keyed by job name.
    /// </summary>
    public static IReadOnlyDictionary<string, JobInfoDto> Jobs { get; } =
        new Dictionary<string, JobInfoDto>(StringComparer.Ordinal)
        {
            [nameof(SyncStravaActivitiesJob)] = new(
                nameof(SyncStravaActivitiesJob),
                "Sync Strava Activities",
                "Synchronize new Strava activities into the local object store."),
            [nameof(SyncStravaActivityDetailJob)] = new(
                nameof(SyncStravaActivityDetailJob),
                "Sync Strava Activity Details",
                "Fetch and cache full activity details (segments, laps, splits) for recent activities."),
            [nameof(SyncStravaActivityStreamsJob)] = new(
                nameof(SyncStravaActivityStreamsJob),
                "Sync Strava Activity Streams",
                "Fetch and cache activity streams (elevation, heartrate, and related time-series data) for recent activities."),
            [nameof(SyncStravaActivityIdImageIdIndexJob)] = new(
                nameof(SyncStravaActivityIdImageIdIndexJob),
                "Sync Strava Activity Image ID Index",
                "Build and refresh the activity-to-image-id index from cached Strava activity details."),
            [nameof(SyncStravaActivityImageJob)] = new(
                nameof(SyncStravaActivityImageJob),
                "Sync Strava Activity Images",
                "Download and cache activity images for recent activities."),
            [JobNames.SyncStravaDataJob] = new(
                JobNames.SyncStravaDataJob,
                "Sync Strava Data",
                "Run the full Strava synchronization pipeline in sequence: activities, details, streams, image index, and images."),
            [nameof(StravaActivitiesExportJob)] = new(
                nameof(StravaActivitiesExportJob),
                "Export Strava Activities",
                "Export Strava activities from object storage to a CSV file."),
            [nameof(CopyObjectStoreJob)] = new(
                nameof(CopyObjectStoreJob),
                "Copy Object Store",
                "Copy object store content from one backing store to another.")
        };
}
