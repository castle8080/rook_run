namespace RookRun.Strava.Auth;

/// <summary>
/// Starts the temporary loopback HTTP listener used for interactive OAuth callbacks.
/// </summary>
public interface IStravaOAuthListenerHost
{
    /// <summary>
    /// Starts a listener session for a single authorization flow.
    /// </summary>
    /// <param name="options">The callback binding and route configuration.</param>
    /// <param name="coordinator">The flow coordinator used to resolve callback results.</param>
    /// <param name="cancellationToken">Cancels listener startup.</param>
    /// <returns>A disposable listener session describing the active callback endpoints.</returns>
    Task<IStravaOAuthListenerSession> StartAsync(
        StravaOAuthListenerOptions options,
        Coordination.StravaOAuthCallbackCoordinator coordinator,
        CancellationToken cancellationToken = default);
}
