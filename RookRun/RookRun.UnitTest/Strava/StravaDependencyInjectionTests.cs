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
    public void AddStravaActivities_RegistersNullTokenStoreWhenDpapiIsDisabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StravaClientOptions.SectionName}:ApiBaseUrl"] = "https://www.strava.com/api/v3",
                [$"{StravaClientOptions.SectionName}:AuthorizationBaseUrl"] = "https://www.strava.com/oauth",
                [$"{StravaClientOptions.SectionName}:ClientId"] = "client-id",
                [$"{StravaClientOptions.SectionName}:ClientSecret"] = "client-secret",
                [$"{StravaClientOptions.SectionName}:UseWindowsDpapi"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddStravaActivities(configuration.GetSection(StravaClientOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var tokenStore = serviceProvider.GetRequiredService<IStravaTokenStore>();

        Assert.IsType<NullStravaTokenStore>(tokenStore);
    }

    [Fact]
    public void AddStravaActivities_RegistersExpectedTokenStoreForCurrentPlatformWhenDpapiIsEnabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StravaClientOptions.SectionName}:ApiBaseUrl"] = "https://www.strava.com/api/v3",
                [$"{StravaClientOptions.SectionName}:AuthorizationBaseUrl"] = "https://www.strava.com/oauth",
                [$"{StravaClientOptions.SectionName}:ClientId"] = "client-id",
                [$"{StravaClientOptions.SectionName}:ClientSecret"] = "client-secret",
                [$"{StravaClientOptions.SectionName}:UseWindowsDpapi"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddStravaActivities(configuration.GetSection(StravaClientOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var tokenStore = serviceProvider.GetRequiredService<IStravaTokenStore>();

        if (OperatingSystem.IsWindows())
        {
            Assert.IsType<WindowsDpapiStravaTokenStore>(tokenStore);
        }
        else
        {
            Assert.IsType<NullStravaTokenStore>(tokenStore);
        }
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
                [$"{StravaClientOptions.SectionName}:ClientSecret"] = "client-secret",
                [$"{StravaClientOptions.SectionName}:UseWindowsDpapi"] = "false"
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
