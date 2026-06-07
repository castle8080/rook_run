namespace RookRun.Strava.Auth;

/// <summary>
/// Provides configuration for persisted Strava token storage.
/// </summary>
public sealed record StravaTokenStoreOptions
{
    /// <summary>
    /// Gets the configuration section name used for Strava token storage settings.
    /// </summary>
    public const string SectionName = "Strava";

    /// <summary>
    /// Gets a value indicating whether Windows DPAPI-backed token persistence is enabled.
    /// </summary>
    public bool UseWindowsDpapi { get; init; }

    /// <summary>
    /// Gets the file path used to store the protected token payload.
    /// </summary>
    public string? FilePath { get; init; }
}
