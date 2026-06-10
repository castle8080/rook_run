using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Strava.Client;
using RookRun.Strava.DependencyInjection;
using RookRun.Strava.Models;
using System.Reflection;
using System.Text.Json;

namespace RookRun.UnitTest.Strava;

public class StravaActivitiesTests
{
    [Fact]
    public void SearchActivitiesAsync_ThrowsWhenPageIsZero()
    {
        var method = typeof(StravaActivitiesClient)
            .GetMethod("BuildActivitiesUri", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var exception = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(null, [new StravaActivityQuery { Page = 0 }]));

        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void SearchActivitiesAsync_ThrowsWhenPerPageIsOutOfRange()
    {
        var method = typeof(StravaActivitiesClient)
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
                [$"{StravaClientOptions.SectionName}:ApiBaseUrl"] = "https://www.strava.com/api/v3",
                [$"{StravaClientOptions.SectionName}:ClientId"] = "client-id",
                [$"{StravaClientOptions.SectionName}:ClientSecret"] = "client-secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddStravaActivities(configuration.GetSection(StravaClientOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var exception = Record.Exception(() => serviceProvider.GetRequiredService<IStravaActivitiesClient>());

        Assert.Null(exception);
    }

    [Fact]
    public void StravaActivity_DeserializesStartAndEndLatLng()
    {
        const string json = """
            {
              "id": 123,
              "start_latlng": [47.6062, -122.3321],
              "end_latlng": [47.6205, -122.3493]
            }
            """;

        var activity = JsonSerializer.Deserialize<StravaActivity>(json);

        Assert.NotNull(activity);
        Assert.Equal([47.6062, -122.3321], activity!.StartLatLng);
        Assert.Equal([47.6205, -122.3493], activity.EndLatLng);
    }
}
