namespace RookRun.Strava.Client;

/// <summary>
/// Provides configuration used by the Strava activities API client.
/// </summary>
public sealed record StravaClientOptions
{
    /// <summary>
    /// Gets the configuration section name for StravaClient settings.
    /// </summary>
    public const string SectionName = "StravaClient";

    /// <summary>
    /// Gets the Strava API base URL.
    /// </summary>
    public required string ApiBaseUrl { get; set; }
}