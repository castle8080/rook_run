namespace RookRun.Strava.Client.Auth;

/// <summary>
/// Represents the persisted Strava token state stored across process invocations.
/// </summary>
public sealed record StravaStoredToken
{
    /// <summary>
    /// Gets the access token used for Strava API calls.
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// Gets the refresh token used to obtain future access tokens.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which the access token expires.
    /// </summary>
    public required DateTimeOffset AccessTokenExpiresAt { get; init; }
}
