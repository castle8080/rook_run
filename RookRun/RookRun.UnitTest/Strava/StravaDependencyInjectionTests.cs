using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Strava.Client.Auth;
using RookRun.Strava.DependencyInjection;
using RookRun.Strava.Options;

namespace RookRun.UnitTest.Strava;

public class StravaDependencyInjectionTests
{
    [Fact]
    public void AddStravaActivities_RegistersNullTokenStoreWhenDpapiIsDisabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StravaOptions.SectionName}:ApiBaseUrl"] = "https://www.strava.com/api/v3",
                [$"{StravaOptions.SectionName}:AuthorizationBaseUrl"] = "https://www.strava.com/oauth",
                [$"{StravaOptions.SectionName}:ClientId"] = "client-id",
                [$"{StravaOptions.SectionName}:ClientSecret"] = "client-secret",
                [$"{StravaOptions.SectionName}:UseWindowsDpapi"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddStravaActivities(configuration.GetSection(StravaOptions.SectionName));

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
                [$"{StravaOptions.SectionName}:ApiBaseUrl"] = "https://www.strava.com/api/v3",
                [$"{StravaOptions.SectionName}:AuthorizationBaseUrl"] = "https://www.strava.com/oauth",
                [$"{StravaOptions.SectionName}:ClientId"] = "client-id",
                [$"{StravaOptions.SectionName}:ClientSecret"] = "client-secret",
                [$"{StravaOptions.SectionName}:UseWindowsDpapi"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddStravaActivities(configuration.GetSection(StravaOptions.SectionName));

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
}
