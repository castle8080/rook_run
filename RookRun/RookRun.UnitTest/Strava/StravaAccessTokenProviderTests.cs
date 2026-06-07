using RookRun.Strava.Auth;
using RookRun.Strava.Auth.Models;

namespace RookRun.UnitTest.Strava;

public class StravaAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_ReturnsStoredValidTokenWithoutCallingOAuthClient()
    {
        var storedToken = new StravaStoredToken
        {
            AccessToken = "stored-access-token",
            RefreshToken = "stored-refresh-token",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        var tokenStore = new FakeStravaTokenStore(storedToken);
        var oauthClient = new FakeStravaOAuthClient();
        using var provider = new StravaAccessTokenProvider(oauthClient, tokenStore);

        var accessToken = await provider.GetAccessTokenAsync();

        Assert.Equal("stored-access-token", accessToken);
        Assert.Equal(0, oauthClient.AcquireCalls);
        Assert.Equal(0, oauthClient.RefreshCalls);
        Assert.Null(tokenStore.LastSavedToken);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RefreshesExpiredStoredTokenAndSavesResult()
    {
        var storedToken = new StravaStoredToken
        {
            AccessToken = "expired-access-token",
            RefreshToken = "stored-refresh-token",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        var refreshedToken = CreateTokenResult("refreshed-access-token", "refreshed-refresh-token", DateTimeOffset.UtcNow.AddMinutes(20));
        var tokenStore = new FakeStravaTokenStore(storedToken);
        var oauthClient = new FakeStravaOAuthClient
        {
            RefreshResult = refreshedToken
        };

        using var provider = new StravaAccessTokenProvider(oauthClient, tokenStore);

        var accessToken = await provider.GetAccessTokenAsync();

        Assert.Equal("refreshed-access-token", accessToken);
        Assert.Equal(0, oauthClient.AcquireCalls);
        Assert.Equal(1, oauthClient.RefreshCalls);
        Assert.Equal("stored-refresh-token", oauthClient.LastRefreshToken);
        Assert.NotNull(tokenStore.LastSavedToken);
        Assert.Equal("refreshed-access-token", tokenStore.LastSavedToken!.AccessToken);
        Assert.Equal("refreshed-refresh-token", tokenStore.LastSavedToken.RefreshToken);
    }

    [Fact]
    public async Task GetAccessTokenAsync_AcquiresTokenWhenRefreshTokenIsUnavailable()
    {
        var acquiredToken = CreateTokenResult("new-access-token", "new-refresh-token", DateTimeOffset.UtcNow.AddMinutes(20));
        var tokenStore = new FakeStravaTokenStore();
        var oauthClient = new FakeStravaOAuthClient
        {
            AcquireResult = acquiredToken
        };

        using var provider = new StravaAccessTokenProvider(oauthClient, tokenStore);

        var accessToken = await provider.GetAccessTokenAsync();

        Assert.Equal("new-access-token", accessToken);
        Assert.Equal(1, oauthClient.AcquireCalls);
        Assert.Equal(0, oauthClient.RefreshCalls);
        Assert.Null(oauthClient.LastRefreshToken);
        Assert.NotNull(tokenStore.LastSavedToken);
        Assert.Equal("new-access-token", tokenStore.LastSavedToken!.AccessToken);
        Assert.Equal("new-refresh-token", tokenStore.LastSavedToken.RefreshToken);
    }

    private static StravaOAuthTokenResult CreateTokenResult(string accessToken, string refreshToken, DateTimeOffset expiresAt) =>
        new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUnixTimeSeconds = expiresAt.ToUnixTimeSeconds(),
            TokenType = "Bearer",
            GrantedScopes = ["activity:read_all"]
        };

    private sealed class FakeStravaOAuthClient : IStravaOAuthClient
    {
        public int AcquireCalls { get; private set; }

        public int RefreshCalls { get; private set; }

        public string? LastRefreshToken { get; private set; }

        public StravaOAuthTokenResult AcquireResult { get; init; } = CreateTokenResult("acquired-access-token", "acquired-refresh-token", DateTimeOffset.UtcNow.AddMinutes(20));

        public StravaOAuthTokenResult RefreshResult { get; init; } = CreateTokenResult("refreshed-access-token", "refreshed-refresh-token", DateTimeOffset.UtcNow.AddMinutes(20));

        public Task<StravaOAuthTokenResult> AcquireTokenAsync(CancellationToken cancellationToken = default)
        {
            AcquireCalls++;
            return Task.FromResult(AcquireResult);
        }

        public Task<StravaOAuthTokenResult> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            LastRefreshToken = refreshToken;

            return Task.FromResult(RefreshResult);
        }
    }

    private sealed class FakeStravaTokenStore : IStravaTokenStore
    {
        private readonly StravaStoredToken? _loadedToken;

        public FakeStravaTokenStore(StravaStoredToken? loadedToken = null)
        {
            _loadedToken = loadedToken;
        }

        public StravaStoredToken? LastSavedToken { get; private set; }

        public Task<StravaStoredToken?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_loadedToken);

        public Task SaveAsync(StravaStoredToken token, CancellationToken cancellationToken = default)
        {
            LastSavedToken = token;
            return Task.CompletedTask;
        }
    }
}
