namespace RookRun.Strava.Auth;

/// <summary>
/// A Strava token store that does not persist any token data.
/// </summary>
public sealed class NullStravaTokenStore : IStravaTokenStore
{
    /// <inheritdoc />
    public Task<StravaStoredToken?> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<StravaStoredToken?>(null);

    /// <inheritdoc />
    public Task SaveAsync(StravaStoredToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Task.CompletedTask;
    }
}
