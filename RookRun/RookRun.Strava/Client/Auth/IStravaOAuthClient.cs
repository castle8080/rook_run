using RookRun.Strava.Client.Auth.Models;

namespace RookRun.Strava.Client.Auth;

/// <summary>
/// Defines the interactive Strava OAuth flow entry point.
/// </summary>
public interface IStravaOAuthClient
{
    /// <summary>
    /// Starts an interactive authorization flow and returns the acquired token payload.
    /// </summary>
    /// <param name="cancellationToken">Cancels the in-progress authorization flow.</param>
    /// <returns>The token result returned from Strava after a successful authorization flow.</returns>
    Task<StravaOAuthTokenResult> AcquireTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an access token using the supplied refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to exchange.</param>
    /// <param name="cancellationToken">Cancels the outbound token refresh request.</param>
    /// <returns>The token result returned from Strava after a successful refresh.</returns>
    Task<StravaOAuthTokenResult> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
