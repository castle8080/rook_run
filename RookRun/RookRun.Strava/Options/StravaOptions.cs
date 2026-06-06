namespace RookRun.Strava.Options;

/// <summary>
/// Provides configuration used by the Strava activities API client.
/// </summary>
public sealed record StravaOptions
{
    /// <summary>
    /// Gets the configuration section name for Strava settings.
    /// </summary>
    public const string SectionName = "Strava";

    /// <summary>
    /// Gets the Strava API base URL.
    /// </summary>
    public required string ApiBaseUrl { get; set; }
}