using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Strava;
using RookRun.Strava.DependencyInjection;
using RookRun.Strava.Models;
using RookRun.Strava.Options;
using System.Reflection;

namespace RookRun.UnitTest.Strava;

public class StravaActivitiesTests
{
    [Fact]
    public void SearchActivitiesAsync_ThrowsWhenPageIsZero()
    {
        var method = typeof(RookRun.Strava.StravaActivities)
            .GetMethod("BuildActivitiesUri", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var exception = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(null, [new StravaActivityQuery { Page = 0 }]));

        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void SearchActivitiesAsync_ThrowsWhenPerPageIsOutOfRange()
    {
        var method = typeof(RookRun.Strava.StravaActivities)
            .GetMethod("BuildActivitiesUri", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var exception = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(null, [new StravaActivityQuery { PerPage = 201 }]));

        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenOptionsAreBoundFromStravaSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StravaOptions.SectionName}:ApiBaseUrl"] = "https://www.strava.com/api/v3",
                [$"{StravaOptions.SectionName}:ClientId"] = "client-id",
                [$"{StravaOptions.SectionName}:ClientSecret"] = "client-secret",
                [$"{StravaOptions.SectionName}:RefreshToken"] = "refresh-token"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddStravaActivities(configuration.GetSection(StravaOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var exception = Record.Exception(() => serviceProvider.GetRequiredService<IStravaActivities>());

        Assert.Null(exception);
    }
}
