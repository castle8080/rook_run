using Microsoft.Extensions.Options;
using RookRun.Strava.Client.Auth;
using System.Runtime.Versioning;

namespace RookRun.UnitTest.Strava;

public class StravaTokenStoreTests
{
    [Fact]
    public async Task NullStravaTokenStore_LoadReturnsNull_AndSaveDoesNotThrow()
    {
        var store = new NullStravaTokenStore();
        var token = new StravaStoredToken
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        var loaded = await store.LoadAsync();
        var exception = await Record.ExceptionAsync(() => store.SaveAsync(token));

        Assert.Null(loaded);
        Assert.Null(exception);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task WindowsDpapiStravaTokenStore_SaveAndLoad_RoundTripsToken()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var filePath = Path.Combine(Path.GetTempPath(), $"strava-token-{Guid.NewGuid():N}.dat");
        var options = Options.Create(new StravaTokenStoreOptions
        {
            UseWindowsDpapi = true,
            FilePath = filePath
        });
        var token = new StravaStoredToken
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        var store = new WindowsDpapiStravaTokenStore(options);

        try
        {
            await store.SaveAsync(token);
            var loaded = await store.LoadAsync();

            Assert.NotNull(loaded);
            Assert.Equal(token.AccessToken, loaded!.AccessToken);
            Assert.Equal(token.RefreshToken, loaded.RefreshToken);
            Assert.Equal(token.AccessTokenExpiresAt, loaded.AccessTokenExpiresAt);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task WindowsDpapiStravaTokenStore_LoadDeletesCorruptFileAndReturnsNull()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var filePath = Path.Combine(Path.GetTempPath(), $"strava-token-{Guid.NewGuid():N}.dat");
        var options = Options.Create(new StravaTokenStoreOptions
        {
            UseWindowsDpapi = true,
            FilePath = filePath
        });
        await File.WriteAllTextAsync(filePath, "not-valid-protected-data");
        var store = new WindowsDpapiStravaTokenStore(options);

        var loaded = await store.LoadAsync();

        Assert.Null(loaded);
        Assert.False(File.Exists(filePath));
    }
}
