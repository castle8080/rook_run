namespace RookRun.Strava.Client.Auth;

/// <summary>
/// Opens the authorization endpoint in the user's browser.
/// </summary>
public interface IStravaAuthorizationLauncher
{
    /// <summary>
    /// Opens the supplied authorization URI.
    /// </summary>
    /// <param name="authorizationUri">The Strava authorization page to open.</param>
    /// <param name="cancellationToken">Cancels browser launch before it begins.</param>
    Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken = default);
}
