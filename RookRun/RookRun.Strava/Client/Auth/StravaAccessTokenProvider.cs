using RookRun.Strava.Client.Auth.Models;

namespace RookRun.Strava.Client.Auth;

/// <summary>
/// Maintains the current Strava access token and refreshes or acquires it when required.
/// </summary>
public sealed class StravaAccessTokenProvider : IStravaAccessTokenProvider, IDisposable
{
    private readonly IStravaOAuthClient _stravaOAuthClient;
    private readonly IStravaTokenStore _tokenStore;
    private readonly SemaphoreSlim _accessTokenLock = new(1, 1);
    private bool _tokenStoreLoaded;

    private string? _refreshToken;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaAccessTokenProvider"/> class.
    /// </summary>
    /// <param name="stravaOAuthClient">The Strava OAuth client used to acquire and refresh tokens.</param>
    /// <param name="tokenStore">The token store used to persist Strava token state.</param>
    public StravaAccessTokenProvider(IStravaOAuthClient stravaOAuthClient, IStravaTokenStore tokenStore)
    {
        _stravaOAuthClient = stravaOAuthClient ?? throw new ArgumentNullException(nameof(stravaOAuthClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
    }

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (HasValidAccessToken())
        {
            return _accessToken!;
        }

        await _accessTokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!_tokenStoreLoaded)
            {
                await LoadStoredTokenAsync(cancellationToken);
            }

            if (HasValidAccessToken())
            {
                return _accessToken!;
            }

            var tokenResponse = await GetFreshTokenAsync(cancellationToken);

            await SetCurrentTokenAsync(tokenResponse, cancellationToken);

            return _accessToken;
        }
        finally
        {
            _accessTokenLock.Release();
        }
    }

    /// <summary>
    /// Releases the synchronization resources used by the access token provider.
    /// </summary>
    public void Dispose()
    {
        _accessTokenLock.Dispose();
    }

    /// <summary>
    /// Determines whether the cached access token can still be used for outgoing API requests.
    /// </summary>
    /// <returns><see langword="true"/> when the cached token is still valid; otherwise, <see langword="false"/>.</returns>
    private bool HasValidAccessToken() =>
        !string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1);

    /// <summary>
    /// Retrieves a fresh token result, preferring refresh-token exchange and falling back to interactive authorization.
    /// </summary>
    /// <param name="cancellationToken">Cancels the token retrieval operation.</param>
    /// <returns>A fresh OAuth token result.</returns>
    private async Task<StravaOAuthTokenResult> GetFreshTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_refreshToken))
        {
            return await _stravaOAuthClient.AcquireTokenAsync(cancellationToken);
        }

        return await _stravaOAuthClient.RefreshAccessTokenAsync(_refreshToken, cancellationToken);
    }

    private async Task LoadStoredTokenAsync(CancellationToken cancellationToken)
    {
        var storedToken = await _tokenStore.LoadAsync(cancellationToken);
        _tokenStoreLoaded = true;

        if (storedToken is null)
        {
            return;
        }

        _accessToken = storedToken.AccessToken;
        _refreshToken = storedToken.RefreshToken;
        _accessTokenExpiresAt = storedToken.AccessTokenExpiresAt;
    }

    private async Task SetCurrentTokenAsync(StravaOAuthTokenResult tokenResponse, CancellationToken cancellationToken)
    {
        _accessToken = tokenResponse.AccessToken;
        _refreshToken = tokenResponse.RefreshToken;
        _accessTokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.ExpiresAtUnixTimeSeconds);

        await _tokenStore.SaveAsync(
            new StravaStoredToken
            {
                AccessToken = _accessToken,
                RefreshToken = _refreshToken,
                AccessTokenExpiresAt = _accessTokenExpiresAt
            },
            cancellationToken);
    }
}
