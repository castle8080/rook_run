namespace RookRun.Job;

/// <summary>
/// Defines public job-name keys used for keyed dependency resolution and API catalog metadata.
/// </summary>
public static class JobNames
{
    /// <summary>
    /// Gets the keyed registration name for the full Strava data synchronization composite job.
    /// </summary>
    public const string SyncStravaDataJob = "SyncStravaDataJob";
}
