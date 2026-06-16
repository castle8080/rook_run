using RookRun.Contracts;
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
            [nameof(ProcessGoogleHealthExportJob)] = new(
                nameof(ProcessGoogleHealthExportJob),
                "Process Google Health Export",
                "Parse Google Health export data and persist normalized workout records."),
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
