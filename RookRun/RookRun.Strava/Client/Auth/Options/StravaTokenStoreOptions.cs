namespace RookRun.Strava.Client.Auth;

/// <summary>
/// Provides configuration for persisted Strava token storage.
/// </summary>
public sealed record StravaTokenStoreOptions
{
    /// <summary>
    /// Gets a value indicating whether Windows DPAPI-backed token persistence is enabled.
    /// </summary>
    public bool UseWindowsDpapi { get; init; }

    /// <summary>
    /// Gets the file path used to store the protected token payload.
    /// </summary>
    public string? FilePath { get; init; }
}
