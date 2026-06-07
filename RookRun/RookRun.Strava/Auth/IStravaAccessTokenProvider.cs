namespace RookRun.Strava.Auth;

/// <summary>
/// Provides access to a valid Strava access token for authenticated API calls.
/// </summary>
public interface IStravaAccessTokenProvider
{
    /// <summary>
    /// Returns a valid Strava access token, acquiring or refreshing it when needed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the token retrieval operation.</param>
    /// <returns>A valid Strava access token.</returns>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
