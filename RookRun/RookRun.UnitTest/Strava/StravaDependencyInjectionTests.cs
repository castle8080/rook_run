using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RookRun.ObjectStore;
using RookRun.Strava.Client;
using RookRun.Strava.Client.Auth;
using RookRun.Strava.DependencyInjection;
using RookRun.Strava.Repositories;

namespace RookRun.UnitTest.Strava;

public class StravaDependencyInjectionTests
{
    [Fact]
    public void AddStravaActivities_RegistersObjectStoreStravaTokenStore()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StravaClientOptions.SectionName}:ApiBaseUrl"] = "https://www.strava.com/api/v3",
                [$"{StravaClientOptions.SectionName}:AuthorizationBaseUrl"] = "https://www.strava.com/oauth",
                [$"{StravaClientOptions.SectionName}:ClientId"] = "client-id",
                [$"{StravaClientOptions.SectionName}:ClientSecret"] = "client-secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IObjectStore>(new InMemoryObjectStore());
        services.AddStravaActivities(configuration.GetSection(StravaClientOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var tokenStore = serviceProvider.GetRequiredService<IStravaTokenStore>();

        Assert.IsType<ObjectStoreStravaTokenStore>(tokenStore);
    }

    [Fact]
    public async Task AddStravaActivities_ObjectStoreStravaTokenStore_UsesFixedTokenPath()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StravaClientOptions.SectionName}:ApiBaseUrl"] = "https://www.strava.com/api/v3",
                [$"{StravaClientOptions.SectionName}:AuthorizationBaseUrl"] = "https://www.strava.com/oauth",
                [$"{StravaClientOptions.SectionName}:ClientId"] = "client-id",
                [$"{StravaClientOptions.SectionName}:ClientSecret"] = "client-secret"
            })
            .Build();

        var objectStore = new InMemoryObjectStore();
        var services = new ServiceCollection();
        services.AddSingleton<IObjectStore>(objectStore);
        services.AddStravaActivities(configuration.GetSection(StravaClientOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var tokenStore = serviceProvider.GetRequiredService<IStravaTokenStore>();
        var expectedToken = new StravaStoredToken
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        await tokenStore.SaveAsync(expectedToken);

        var storedToken = await objectStore.TryReadObjectAsync<StravaStoredToken>("secrets/strava/auth_token.json.br");

        Assert.IsType<ObjectStoreStravaTokenStore>(tokenStore);
        Assert.True(storedToken.IsFound);
        Assert.NotNull(storedToken.Value);
        Assert.Equal(expectedToken.AccessToken, storedToken.Value!.AccessToken);
        Assert.Equal(expectedToken.RefreshToken, storedToken.Value.RefreshToken);
    }

    [Fact]
    public void AddStravaActivities_RegistersObjectStoreStravaActivitiesRepository()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StravaClientOptions.SectionName}:ApiBaseUrl"] = "https://www.strava.com/api/v3",
                [$"{StravaClientOptions.SectionName}:AuthorizationBaseUrl"] = "https://www.strava.com/oauth",
                [$"{StravaClientOptions.SectionName}:ClientId"] = "client-id",
                [$"{StravaClientOptions.SectionName}:ClientSecret"] = "client-secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IObjectStore>(new InMemoryObjectStore());
        services.AddStravaActivities(configuration.GetSection(StravaClientOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetRequiredService<IStravaActivitiesRepository>();

        Assert.IsType<ObjectStoreStravaActivitiesRepository>(repository);
    }
}
