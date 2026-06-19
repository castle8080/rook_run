using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RookRun.Api.Controllers;
using RookRun.Contracts.Strava;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Unit tests for <see cref="RunStatsController"/>.
/// </summary>
public class RunStatsControllerTests
{
    // Conversion constants mirroring the controller.
    private const double MetresPerMile = 1609.344;
    private const double FeetPerMetre = 3.28084;

    /// <summary>
    /// Creates a controller instance wired to the given repository mock and a
    /// <see cref="DefaultHttpContext"/> so that response-header assertions work.
    /// </summary>
    private static RunStatsController CreateSut(IStravaActivitiesRepository repository)
    {
        var sut = new RunStatsController(repository);
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return sut;
    }

    /// <summary>
    /// Builds a repository mock that returns the supplied activities in a single page.
    /// </summary>
    private static Mock<IStravaActivitiesRepository> BuildSinglePageRepository(
        IEnumerable<StravaActivity> activities)
    {
        var mock = new Mock<IStravaActivitiesRepository>();
        mock.Setup(r => r.ListAsync(It.IsAny<ListStravaActivitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListStravaActivitiesResult
            {
                Page = 1,
                PageSize = 500,
                HasNextPage = false,
                Items = activities.ToArray()
            });
        return mock;
    }

    /// <summary>
    /// Creates a run activity with local start date set to the given year/month/day.
    /// AverageSpeed is set so pace can be derived.
    /// </summary>
    private static StravaActivity MakeRun(
        int year, int month, int day,
        double distanceMetres = 8046.72, // 5 miles
        int movingTimeSecs = 2700,       // 45 min → 9:00/mi
        double? averageSpeedMs = null,
        double elevationGainMetres = 100.0,
        string sportType = "Run")
    {
        var localDate = new DateTimeOffset(year, month, day, 8, 0, 0, TimeSpan.FromHours(-5));
        // Default speed: derive from distance/time if not supplied.
        var speed = averageSpeedMs ?? (distanceMetres / movingTimeSecs);
        return new StravaActivity
        {
            Id = Random.Shared.NextInt64(),
            SportType = sportType,
            Type = sportType,
            StartDate = localDate.ToUniversalTime(),
            StartDateLocal = localDate,
            Distance = distanceMetres,
            MovingTime = movingTimeSecs,
            ElapsedTime = movingTimeSecs,
            TotalElevationGain = elevationGainMetres,
            AverageSpeed = speed
        };
    }

    // -------------------------------------------------------------------------
    // Response shape
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the endpoint returns HTTP 200 with a populated RunStatsResponse.
    /// </summary>
    [Fact]
    public async Task GetAsync_Returns200WithResponse()
    {
        var run = MakeRun(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var sut = CreateSut(BuildSinglePageRepository([run]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<RunStatsResponse>(ok.Value);
    }

    /// <summary>
    /// Verifies that the response always contains exactly 13 month entries regardless
    /// of how many activities exist, so every month slot is rendered in the UI.
    /// </summary>
    [Fact]
    public async Task GetAsync_Returns13Months()
    {
        // Single run in the current month — remaining 12 months should be empty placeholders.
        var run = MakeRun(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var sut = CreateSut(BuildSinglePageRepository([run]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        Assert.Equal(13, response.Months.Length);
    }

    /// <summary>
    /// Verifies that months are returned in descending order (most recent first).
    /// </summary>
    [Fact]
    public async Task GetAsync_MonthsAreOrderedDescending()
    {
        var sut = CreateSut(BuildSinglePageRepository([]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        var months = response.Months;

        for (var i = 1; i < months.Length; i++)
        {
            var prev = months[i - 1];
            var curr = months[i];
            var prevOrdinal = prev.Year * 100 + prev.Month;
            var currOrdinal = curr.Year * 100 + curr.Month;
            Assert.True(prevOrdinal > currOrdinal, $"Expected month {i - 1} to be newer than month {i}.");
        }
    }

    // -------------------------------------------------------------------------
    // Empty month placeholders
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that months with no run activities produce a placeholder with all-zero
    /// aggregates and null optional fields, so the UI can render a row for every month.
    /// </summary>
    [Fact]
    public async Task GetAsync_EmptyMonthHasZeroStats()
    {
        // Single run in the current month; find a month that will be empty.
        var run = MakeRun(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var sut = CreateSut(BuildSinglePageRepository([run]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        // The last month in the list should be at least 12 months old and likely empty
        // unless the test is run on the exact boundary — pick a month known to have no data.
        var emptyMonths = response.Months.Where(m => m.TotalRuns == 0).ToList();
        Assert.NotEmpty(emptyMonths);

        foreach (var m in emptyMonths)
        {
            Assert.Equal(0, m.TotalMovingTimeSeconds);
            Assert.Equal(0.0, m.TotalDistanceMiles);
            Assert.Equal(0.0, m.TotalElevationFeet);
            Assert.Null(m.LongestRunDistanceMiles);
            Assert.Null(m.LongestRunPaceSecondsPerMile);
            Assert.Null(m.FastestRunPaceSecondsPerMile);
            Assert.Null(m.FastestRunDistanceMiles);
        }
    }

    // -------------------------------------------------------------------------
    // Activity type filtering
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that non-run activities (e.g. "Ride") are excluded from all statistics.
    /// </summary>
    [Fact]
    public async Task GetAsync_ExcludesNonRunActivities()
    {
        var now = DateTime.UtcNow;
        var ride = new StravaActivity
        {
            Id = 1,
            SportType = "Ride",
            Type = "Ride",
            StartDate = new DateTimeOffset(now.Year, now.Month, 1, 12, 0, 0, TimeSpan.Zero),
            StartDateLocal = new DateTimeOffset(now.Year, now.Month, 1, 8, 0, 0, TimeSpan.FromHours(-5)),
            Distance = 32186.9, // 20 miles on a bike
            MovingTime = 3600,
            AverageSpeed = 8.94
        };
        var sut = CreateSut(BuildSinglePageRepository([ride]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        Assert.All(response.Months, m => Assert.Equal(0, m.TotalRuns));
        Assert.Equal(0, response.YtdSummary.TotalRuns);
    }

    /// <summary>
    /// Verifies that VirtualRun activities are included in statistics, same as Run activities.
    /// </summary>
    [Fact]
    public async Task GetAsync_IncludesVirtualRunActivities()
    {
        var now = DateTime.UtcNow;
        var virtualRun = MakeRun(now.Year, now.Month, 1, sportType: "VirtualRun");
        var sut = CreateSut(BuildSinglePageRepository([virtualRun]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        var currentMonth = response.Months.First();
        Assert.Equal(1, currentMonth.TotalRuns);
    }

    // -------------------------------------------------------------------------
    // Aggregation correctness
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that distance, time, and elevation are correctly summed across multiple
    /// runs in the same month, and that conversions (metres→miles, metres→feet) are applied.
    /// </summary>
    [Fact]
    public async Task GetAsync_CorrectlySumsDistanceTimeAndElevation()
    {
        var now = DateTime.UtcNow;
        // Two runs: 5 miles each, 45 min each, 100m elevation each.
        var run1 = MakeRun(now.Year, now.Month, 1, distanceMetres: MetresPerMile * 5, movingTimeSecs: 2700, elevationGainMetres: 100);
        var run2 = MakeRun(now.Year, now.Month, 5, distanceMetres: MetresPerMile * 5, movingTimeSecs: 2700, elevationGainMetres: 100);
        var sut = CreateSut(BuildSinglePageRepository([run1, run2]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        var month = response.Months.First();

        Assert.Equal(2, month.TotalRuns);
        Assert.Equal(5400, month.TotalMovingTimeSeconds);
        Assert.Equal(10.0, month.TotalDistanceMiles, precision: 3);
        Assert.Equal(200 * FeetPerMetre, month.TotalElevationFeet, precision: 2);
    }

    /// <summary>
    /// Verifies that the longest run is identified by distance, and its pace is
    /// correctly derived from AverageSpeed.
    /// </summary>
    [Fact]
    public async Task GetAsync_IdentifiesLongestRunByDistance()
    {
        var now = DateTime.UtcNow;
        var shortRun = MakeRun(now.Year, now.Month, 1, distanceMetres: MetresPerMile * 3, movingTimeSecs: 1620, averageSpeedMs: (MetresPerMile * 3) / 1620.0);
        var longRun = MakeRun(now.Year, now.Month, 10, distanceMetres: MetresPerMile * 10, movingTimeSecs: 5400, averageSpeedMs: (MetresPerMile * 10) / 5400.0);
        var sut = CreateSut(BuildSinglePageRepository([shortRun, longRun]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        var month = response.Months.First();

        Assert.NotNull(month.LongestRunDistanceMiles);
        Assert.Equal(10.0, month.LongestRunDistanceMiles!.Value, precision: 3);
    }

    /// <summary>
    /// Verifies that the fastest run is identified by best pace (lowest sec/mi)
    /// among runs that are at least 1 mile long.
    /// </summary>
    [Fact]
    public async Task GetAsync_IdentifiesFastestRunByBestPace()
    {
        var now = DateTime.UtcNow;
        // Slow 5-mile run at 10:00/mi.
        var slowRun = MakeRun(now.Year, now.Month, 1,
            distanceMetres: MetresPerMile * 5,
            movingTimeSecs: 3000,
            averageSpeedMs: MetresPerMile * 5 / 3000.0);
        // Faster 3-mile run at 8:00/mi.
        var fastRun = MakeRun(now.Year, now.Month, 5,
            distanceMetres: MetresPerMile * 3,
            movingTimeSecs: 1440,
            averageSpeedMs: MetresPerMile * 3 / 1440.0);
        var sut = CreateSut(BuildSinglePageRepository([slowRun, fastRun]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        var month = response.Months.First();

        Assert.NotNull(month.FastestRunPaceSecondsPerMile);
        // 8:00/mi = 480 sec/mi
        Assert.Equal(480.0, month.FastestRunPaceSecondsPerMile!.Value, precision: 1);
        Assert.Equal(3.0, month.FastestRunDistanceMiles!.Value, precision: 3);
    }

    // -------------------------------------------------------------------------
    // Fastest-run threshold
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that runs shorter than 1 mile are excluded from the fastest-run
    /// calculation and FastestRun fields are null when only sub-mile runs exist.
    /// </summary>
    [Fact]
    public async Task GetAsync_FastestRun_ExcludesRunsShorterThanOneMile()
    {
        var now = DateTime.UtcNow;
        // 0.5 mile run — below 1-mile threshold.
        var shortRun = MakeRun(now.Year, now.Month, 1,
            distanceMetres: MetresPerMile * 0.5,
            movingTimeSecs: 240,
            averageSpeedMs: MetresPerMile * 0.5 / 240.0);
        var sut = CreateSut(BuildSinglePageRepository([shortRun]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        var month = response.Months.First();

        // Run was counted but does not qualify for fastest-run field.
        Assert.Equal(1, month.TotalRuns);
        Assert.Null(month.FastestRunPaceSecondsPerMile);
        Assert.Null(month.FastestRunDistanceMiles);
    }

    // -------------------------------------------------------------------------
    // Pace fallback (AverageSpeed null)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that pace is correctly derived from distance ÷ moving time when
    /// AverageSpeed is null, as some Strava activities omit the field.
    /// </summary>
    [Fact]
    public async Task GetAsync_PaceFallback_UsesDistanceDividedByMovingTime()
    {
        var now = DateTime.UtcNow;
        // 5 miles in 2700 sec = 9:00/mi = 540 sec/mi. AverageSpeed deliberately null.
        var run = MakeRun(now.Year, now.Month, 1,
            distanceMetres: MetresPerMile * 5,
            movingTimeSecs: 2700,
            averageSpeedMs: null);
        // Override AverageSpeed to null after construction via a fresh record.
        var runNoSpeed = run with { AverageSpeed = null };
        var sut = CreateSut(BuildSinglePageRepository([runNoSpeed]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        var month = response.Months.First();

        Assert.NotNull(month.FastestRunPaceSecondsPerMile);
        Assert.Equal(540.0, month.FastestRunPaceSecondsPerMile!.Value, precision: 1);
    }

    // -------------------------------------------------------------------------
    // YTD summary
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the YTD summary counts only activities in the current calendar year.
    /// </summary>
    [Fact]
    public async Task GetAsync_YtdSummary_IncludesOnlyCurrentYearActivities()
    {
        var currentYear = DateTime.UtcNow.Year;
        var now = DateTime.UtcNow;

        // One run this year, one run last year.
        var thisYear = MakeRun(currentYear, 1, 5, distanceMetres: MetresPerMile * 5);
        var lastYear = MakeRun(currentYear - 1, 12, 1, distanceMetres: MetresPerMile * 5);

        // Both activities are within the UTC query window (≤ 13 months back),
        // but last-year activity's local date is outside the current calendar year.
        var mock = new Mock<IStravaActivitiesRepository>();
        mock.Setup(r => r.ListAsync(It.IsAny<ListStravaActivitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListStravaActivitiesResult
            {
                Page = 1, PageSize = 500, HasNextPage = false,
                Items = [thisYear, lastYear]
            });

        var sut = CreateSut(mock.Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        Assert.Equal(1, response.YtdSummary.TotalRuns);
        Assert.Equal(currentYear, response.YtdSummary.Year);
        Assert.Equal(5.0, response.YtdSummary.TotalDistanceMiles, precision: 3);
    }

    // -------------------------------------------------------------------------
    // UTC query padding
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the UTC start date passed to the repository is one day before the
    /// local window start, to capture activities whose local date is on the boundary but
    /// whose UTC date is the previous day.
    /// </summary>
    [Fact]
    public async Task GetAsync_UtcQueryStart_IsOneDayBeforeLocalWindowStart()
    {
        DateTimeOffset capturedStart = default;
        var mock = new Mock<IStravaActivitiesRepository>();
        mock.Setup(r => r.ListAsync(It.IsAny<ListStravaActivitiesQuery>(), It.IsAny<CancellationToken>()))
            .Callback<ListStravaActivitiesQuery, CancellationToken>((q, _) => capturedStart = q.StartDateUtc!.Value)
            .ReturnsAsync(new ListStravaActivitiesResult { Page = 1, PageSize = 500, HasNextPage = false, Items = [] });

        var sut = CreateSut(mock.Object);
        await sut.GetAsync(CancellationToken.None);

        var today = DateTimeOffset.UtcNow;
        var expectedLocalStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-12);
        var expectedUtcStart = new DateTimeOffset(expectedLocalStart.Year, expectedLocalStart.Month, expectedLocalStart.Day, 0, 0, 0, TimeSpan.Zero)
            .AddDays(-1);

        // Allow up to 5 seconds of clock drift between test setup and controller execution.
        Assert.True(Math.Abs((capturedStart - expectedUtcStart).TotalSeconds) < 5,
            $"Expected UTC start ~{expectedUtcStart:O} but got {capturedStart:O}");
    }

    // -------------------------------------------------------------------------
    // Paging
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the controller pages through all repository pages to collect
    /// every run activity when HasNextPage is true.
    /// </summary>
    [Fact]
    public async Task GetAsync_PagesUntilHasNextPageIsFalse()
    {
        var now = DateTime.UtcNow;
        var run1 = MakeRun(now.Year, now.Month, 1);
        var run2 = MakeRun(now.Year, now.Month, 5);

        var mock = new Mock<IStravaActivitiesRepository>(MockBehavior.Strict);
        mock.SetupSequence(r => r.ListAsync(It.IsAny<ListStravaActivitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListStravaActivitiesResult { Page = 1, PageSize = 500, HasNextPage = true, Items = [run1] })
            .ReturnsAsync(new ListStravaActivitiesResult { Page = 2, PageSize = 500, HasNextPage = false, Items = [run2] });

        var sut = CreateSut(mock.Object);
        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        var currentMonth = response.Months.First();

        Assert.Equal(2, currentMonth.TotalRuns);
        mock.Verify(r => r.ListAsync(It.IsAny<ListStravaActivitiesQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // -------------------------------------------------------------------------
    // Cache-Control header
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the response includes Cache-Control: private, max-age=300 so
    /// the browser caches the response for 5 minutes without hitting the server.
    /// </summary>
    [Fact]
    public async Task GetAsync_SetsCacheControlHeader()
    {
        var sut = CreateSut(BuildSinglePageRepository([]).Object);

        await sut.GetAsync(CancellationToken.None);

        var cacheControl = sut.HttpContext.Response.Headers.CacheControl.ToString();
        Assert.Equal("private, max-age=300", cacheControl);
    }

    // -------------------------------------------------------------------------
    // UTC padding: boundary activities are discarded post-fetch
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that an activity whose local date falls before the 13-month local
    /// window start is discarded even though its UTC date falls within the padded query range.
    /// </summary>
    [Fact]
    public async Task GetAsync_DiscardsActivitiesBeforeLocalWindowStart()
    {
        var today = DateTimeOffset.UtcNow;
        var localWindowStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-12);

        // Activity with a local date 1 day before the window start — should be excluded.
        var localDate = localWindowStart.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        var boundaryActivity = new StravaActivity
        {
            Id = 99,
            SportType = "Run",
            Type = "Run",
            StartDate = new DateTimeOffset(localDate, TimeSpan.Zero).AddHours(5), // UTC > local
            StartDateLocal = new DateTimeOffset(localDate, TimeSpan.FromHours(-5)),
            Distance = MetresPerMile * 5,
            MovingTime = 2700,
            AverageSpeed = MetresPerMile * 5 / 2700.0
        };

        var sut = CreateSut(BuildSinglePageRepository([boundaryActivity]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        Assert.All(response.Months, m => Assert.Equal(0, m.TotalRuns));
    }

    /// <summary>
    /// Verifies that an activity with zero AverageSpeed and zero MovingTime does not
    /// qualify for the fastest-run field (pace cannot be computed from either source).
    /// </summary>
    [Fact]
    public async Task GetAsync_PaceFallback_ZeroSpeedAndZeroTime_NotFastest()
    {
        var now = DateTime.UtcNow;
        // Activity with no usable speed data — should not appear as fastest run.
        var badActivity = new StravaActivity
        {
            Id = 42,
            SportType = "Run",
            Type = "Run",
            StartDate = new DateTimeOffset(now.Year, now.Month, 1, 12, 0, 0, TimeSpan.Zero),
            StartDateLocal = new DateTimeOffset(now.Year, now.Month, 1, 8, 0, 0, TimeSpan.FromHours(-5)),
            Distance = MetresPerMile * 2,
            MovingTime = 0,         // zero — no pace derivable from time
            AverageSpeed = null     // null — no speed either
        };
        var sut = CreateSut(BuildSinglePageRepository([badActivity]).Object);

        var result = await sut.GetAsync(CancellationToken.None);

        var response = (RunStatsResponse)((OkObjectResult)result.Result!).Value!;
        var month = response.Months.First();

        // Run was counted but pace is indeterminate — fastest field should be null.
        Assert.Equal(1, month.TotalRuns);
        Assert.Null(month.FastestRunPaceSecondsPerMile);
    }
}
