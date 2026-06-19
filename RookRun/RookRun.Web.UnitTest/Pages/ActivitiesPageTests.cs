using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Contracts.Strava;
using RookRun.Web.Pages;
using RookRun.Web.Services;
using RookRun.Web.UnitTest.Infrastructure;

namespace RookRun.Web.UnitTest.Pages;

/// <summary>
/// Tests for the activities page component.
/// </summary>
public sealed class ActivitiesPageTests : TestContext
{
    /// <summary>
    /// Verifies loading activities renders data rows and summary totals.
    /// </summary>
    [Fact]
    public void LoadActivitiesAsync_RendersLoadedActivitiesAndSummary()
    {
        var handler = new StubHttpMessageHandler();
        handler.AddResponse(
            request => request.Method == HttpMethod.Get && request.RequestUri?.PathAndQuery.StartsWith("/api/strava/activities", StringComparison.OrdinalIgnoreCase) == true,
            () => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new ListStravaActivitiesResponse
                {
                    Page = 1,
                    PageSize = 1000,
                    HasNextPage = false,
                    Items =
                    [
                        new StravaActivityDto
                        {
                            Id = 1,
                            Name = "Morning Run",
                            Type = "Run",
                            StartDate = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
                            StartDateLocal = new DateTimeOffset(2026, 6, 1, 6, 0, 0, TimeSpan.FromHours(-6)),
                            ElapsedTime = 1800,
                            MovingTime = 1700,
                            Distance = 5000,
                            TotalElevationGain = 40,
                            AverageHeartrate = 150,
                            MaxHeartrate = 168
                        },
                        new StravaActivityDto
                        {
                            Id = 2,
                            Name = "Evening Ride",
                            Type = "Ride",
                            StartDate = new DateTimeOffset(2026, 6, 2, 23, 0, 0, TimeSpan.Zero),
                            StartDateLocal = new DateTimeOffset(2026, 6, 2, 17, 0, 0, TimeSpan.FromHours(-6)),
                            ElapsedTime = 3600,
                            MovingTime = 3400,
                            Distance = 20000,
                            TotalElevationGain = 200,
                            AverageHeartrate = 138,
                            MaxHeartrate = 162
                        }
                    ]
                })
            });

        RegisterActivitiesClient(handler);

        var cut = RenderComponent<Activities>();

        cut.Find("button.btn.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Morning Run", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Evening Ride", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Showing 2 of 2 loaded activities.", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Verifies an invalid date range is rejected before any API call is made.
    /// </summary>
    [Fact]
    public void LoadActivitiesAsync_ShowsValidationErrorWhenStartDateIsAfterEndDate()
    {
        var handler = new StubHttpMessageHandler();
        RegisterActivitiesClient(handler);

        var cut = RenderComponent<Activities>();

        cut.Find("#startDate").Change("2026-06-15");
        cut.Find("#endDate").Change("2026-06-10");
        cut.Find("button.btn.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Start date must be less than or equal to end date.", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(0, handler.RequestCount);
        });
    }

    /// <summary>
    /// Verifies type filter selection reduces the rendered data rows.
    /// </summary>
    [Fact]
    public void SelectedTypeFilter_UpdatesRenderedRows()
    {
        var handler = new StubHttpMessageHandler();
        handler.AddResponse(
            request => request.Method == HttpMethod.Get && request.RequestUri?.PathAndQuery.StartsWith("/api/strava/activities", StringComparison.OrdinalIgnoreCase) == true,
            () => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new ListStravaActivitiesResponse
                {
                    Page = 1,
                    PageSize = 1000,
                    HasNextPage = false,
                    Items =
                    [
                        new StravaActivityDto { Id = 10, Name = "Run Session", Type = "Run", StartDate = DateTimeOffset.UtcNow, StartDateLocal = DateTimeOffset.UtcNow },
                        new StravaActivityDto { Id = 20, Name = "Ride Session", Type = "Ride", StartDate = DateTimeOffset.UtcNow, StartDateLocal = DateTimeOffset.UtcNow }
                    ]
                })
            });

        RegisterActivitiesClient(handler);

        var cut = RenderComponent<Activities>();
        cut.Find("button.btn.btn-primary").Click();

        cut.WaitForAssertion(() => Assert.Contains("Showing 2 of 2 loaded activities.", cut.Markup, StringComparison.Ordinal));

        cut.Find("#typeFilter").Change("Run");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Showing 1 of 2 loaded activities.", cut.Markup, StringComparison.Ordinal);
            var rows = cut.FindAll("tbody tr");
            Assert.Single(rows);
            Assert.Contains("Run Session", rows[0].TextContent, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Verifies downloading CSV invokes the expected JavaScript interop call.
    /// </summary>
    [Fact]
    public void DownloadCsvAsync_InvokesDownloadJavaScriptInterop()
    {
        var handler = new StubHttpMessageHandler();
        handler.AddResponse(
            request => request.Method == HttpMethod.Get && request.RequestUri?.PathAndQuery.StartsWith("/api/strava/activities", StringComparison.OrdinalIgnoreCase) == true,
            () => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new ListStravaActivitiesResponse
                {
                    Page = 1,
                    PageSize = 1000,
                    HasNextPage = false,
                    Items =
                    [
                        new StravaActivityDto
                        {
                            Id = 33,
                            Name = "CSV Run",
                            Type = "Run",
                            StartDate = new DateTimeOffset(2026, 6, 10, 13, 0, 0, TimeSpan.Zero),
                            StartDateLocal = new DateTimeOffset(2026, 6, 10, 7, 0, 0, TimeSpan.FromHours(-6)),
                            ElapsedTime = 1800,
                            MovingTime = 1750,
                            Distance = 5000,
                            TotalElevationGain = 50,
                            AverageHeartrate = 152,
                            MaxHeartrate = 169
                        }
                    ]
                })
            });

        RegisterActivitiesClient(handler);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<Activities>();
        cut.Find("button.btn.btn-primary").Click();
        cut.WaitForAssertion(() => Assert.Contains("Showing 1 of 1 loaded activities.", cut.Markup, StringComparison.Ordinal));

        cut.Find("button.btn.btn-outline-secondary").Click();

        cut.WaitForAssertion(() =>
        {
            var invocation = JSInterop.Invocations.Single(call =>
                string.Equals(call.Identifier, "rookRunDownloads.downloadTextFile", StringComparison.Ordinal));

            Assert.Equal(3, invocation.Arguments.Count);
            Assert.Contains("strava_activities_", invocation.Arguments[0]?.ToString(), StringComparison.Ordinal);
            Assert.Contains("StartDateUtc", invocation.Arguments[1]?.ToString(), StringComparison.Ordinal);
            Assert.Equal("text/csv;charset=utf-8", invocation.Arguments[2]?.ToString());
        });
    }

    /// <summary>
    /// Registers the typed activities API client with a deterministic HTTP transport.
    /// </summary>
    /// <param name="handler">The fake HTTP transport used by the client.</param>
    private void RegisterActivitiesClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        Services.AddSingleton(new StravaActivitiesApiClient(httpClient));
    }
}