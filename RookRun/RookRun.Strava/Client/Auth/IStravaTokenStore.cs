namespace RookRun.Strava.Client.Auth;

/// <summary>
/// Loads and saves persisted Strava token data.
/// </summary>
public interface IStravaTokenStore
{
    /// <summary>
    /// Loads the persisted Strava token, when available.
    /// </summary>
    /// <param name="cancellationToken">Cancels the load operation.</param>
    /// <returns>The persisted token, or <see langword="null"/> when none is available.</returns>
    Task<StravaStoredToken?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the supplied Strava token data.
    /// </summary>
    /// <param name="token">The token data to persist.</param>
    /// <param name="cancellationToken">Cancels the save operation.</param>
    Task SaveAsync(StravaStoredToken token, CancellationToken cancellationToken = default);
}
