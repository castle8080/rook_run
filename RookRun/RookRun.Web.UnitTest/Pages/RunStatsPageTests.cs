using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Contracts.Strava;
using RookRun.Web.Pages;
using RookRun.Web.Services;
using RookRun.Web.UnitTest.Infrastructure;

namespace RookRun.Web.UnitTest.Pages;

/// <summary>
/// Tests for the top-level RunStats page component.
/// </summary>
public sealed class RunStatsPageTests : TestContext
{
    /// <summary>
    /// Verifies the page loads run stats on initialization and renders key summary and month content.
    /// </summary>
    [Fact]
    public void OnInitializedAsync_RendersRunStats_WhenApiSucceeds()
    {
        var handler = new StubHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "api/strava/run-stats", new RunStatsResponse
        {
            YtdSummary = new RunYtdSummaryDto
            {
                Year = 2026,
                TotalRuns = 48,
                TotalDistanceMiles = 312.4
            },
            Months =
            [
                new RunMonthStatsDto
                {
                    Year = 2026,
                    Month = 6,
                    TotalRuns = 8,
                    TotalMovingTimeSeconds = 33120,
                    TotalDistanceMiles = 62.3,
                    TotalElevationFeet = 3420,
                    LongestRunDistanceMiles = 14.2,
                    LongestRunPaceSecondsPerMile = 552,
                    FastestRunPaceSecondsPerMile = 542,
                    FastestRunDistanceMiles = 6.2
                }
            ]
        });

        RegisterActivitiesClient(handler);

        var cut = RenderComponent<RunStats>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2026 Year to Date", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("312.4 mi", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("June 2026", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("9:02/mi", cut.Markup, StringComparison.Ordinal);
        });

        Assert.Equal(1, handler.RequestCount);
    }

    /// <summary>
    /// Verifies API failures are surfaced as user-visible load errors.
    /// </summary>
    [Fact]
    public void OnInitializedAsync_RendersError_WhenApiFails()
    {
        var handler = new StubHttpMessageHandler();
        handler.AddResponse(
            request => request.Method == HttpMethod.Get &&
                       request.RequestUri?.PathAndQuery.Equals("/api/strava/run-stats", StringComparison.OrdinalIgnoreCase) == true,
            () => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        RegisterActivitiesClient(handler);

        var cut = RenderComponent<RunStats>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed to load run stats", cut.Markup, StringComparison.Ordinal);
        });

        Assert.Equal(1, handler.RequestCount);
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
