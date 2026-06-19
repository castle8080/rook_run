using Microsoft.AspNetCore.Mvc;
using RookRun.Contracts.Strava;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.Api.Controllers;

/// <summary>
/// Exposes aggregated run statistics endpoints.
/// </summary>
[ApiController]
[Route("api/strava/run-stats")]
public sealed class RunStatsController : ControllerBase
{
    /// <summary>
    /// Conversion factor: metres per mile.
    /// </summary>
    private const double MetresPerMile = 1609.344;

    /// <summary>
    /// Conversion factor: feet per metre.
    /// </summary>
    private const double FeetPerMetre = 3.28084;

    /// <summary>
    /// Minimum distance in metres for a run to qualify for the fastest-run calculation.
    /// </summary>
    private const double MinFastestRunMetres = MetresPerMile; // 1 mile

    /// <summary>
    /// The run sport types that are included in statistics.
    /// </summary>
    private static readonly HashSet<string> RunSportTypes =
        new(StringComparer.OrdinalIgnoreCase) { "Run", "VirtualRun" };

    private readonly IStravaActivitiesRepository stravaActivitiesRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunStatsController"/> class.
    /// </summary>
    /// <param name="stravaActivitiesRepository">The repository used to query persisted Strava activities.</param>
    public RunStatsController(IStravaActivitiesRepository stravaActivitiesRepository)
    {
        this.stravaActivitiesRepository = stravaActivitiesRepository
            ?? throw new ArgumentNullException(nameof(stravaActivitiesRepository));
    }

    /// <summary>
    /// Returns aggregated monthly run statistics for a 13-month rolling window ending today,
    /// plus a year-to-date summary for the current calendar year.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <returns>A <see cref="RunStatsResponse"/> with YTD totals and per-month stats.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(RunStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RunStatsResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var today = DateTimeOffset.UtcNow;

        // The local window starts at the 1st of the month 12 months before the current month.
        var localWindowStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-12);

        // The UTC query start is pushed back 1 day to capture activities whose local date
        // falls on the window-start month boundary but whose UTC date is the previous day
        // (e.g. a runner whose local offset is UTC-8 starts a run at 23:00 local = 07:00 UTC next day).
        var startUtc = new DateTimeOffset(localWindowStart.Year, localWindowStart.Month, localWindowStart.Day, 0, 0, 0, TimeSpan.Zero)
            .AddDays(-1);

        var activities = await FetchAllRunActivitiesAsync(startUtc, today, cancellationToken);

        // Discard any activities whose local start date falls before the window start month.
        var runActivities = activities
            .Where(a => a.StartDateLocal.HasValue
                && a.StartDateLocal.Value.Date >= localWindowStart.ToDateTime(TimeOnly.MinValue))
            .ToList();

        var response = BuildResponse(runActivities, today, localWindowStart);

        Response.Headers.CacheControl = "private, max-age=300";

        return Ok(response);
    }

    /// <summary>
    /// Pages through the repository to fetch all run activities within the UTC date range.
    /// Activities are not filtered by sport type at query time; sport-type filtering is applied
    /// after fetching because the repository supports only a single-value type filter.
    /// </summary>
    /// <param name="startUtc">The inclusive UTC lower bound.</param>
    /// <param name="endUtc">The inclusive UTC upper bound.</param>
    /// <param name="cancellationToken">A token used to cancel paging.</param>
    /// <returns>All run and virtual-run activities within the date range.</returns>
    private async Task<List<StravaActivity>> FetchAllRunActivitiesAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken)
    {
        const int pageSize = 500;
        var page = 1;
        var results = new List<StravaActivity>();

        while (true)
        {
            var result = await this.stravaActivitiesRepository.ListAsync(
                new ListStravaActivitiesQuery
                {
                    Page = page,
                    PageSize = pageSize,
                    StartDateUtc = startUtc,
                    EndDateUtc = endUtc,
                    SortDirection = StravaActivitiesSortDirection.Desc
                },
                cancellationToken);

            foreach (var activity in result.Items)
            {
                var sportType = activity.SportType ?? activity.Type;
                if (sportType is not null && RunSportTypes.Contains(sportType))
                {
                    results.Add(activity);
                }
            }

            if (!result.HasNextPage)
            {
                break;
            }

            page++;
        }

        return results;
    }

    /// <summary>
    /// Builds the full <see cref="RunStatsResponse"/> from the fetched run activities.
    /// Generates placeholder entries for months with zero runs so every month in the
    /// 13-month window appears in the response.
    /// </summary>
    /// <param name="runActivities">All run activities in the local window.</param>
    /// <param name="today">The current UTC date used to derive the current year and month.</param>
    /// <param name="localWindowStart">The first day of the earliest month in the window.</param>
    /// <returns>The populated response.</returns>
    private static RunStatsResponse BuildResponse(
        List<StravaActivity> runActivities,
        DateTimeOffset today,
        DateOnly localWindowStart)
    {
        // Group activities by (year, month) using local start date.
        var byMonth = runActivities
            .Where(a => a.StartDateLocal.HasValue)
            .GroupBy(a => (a.StartDateLocal!.Value.Year, a.StartDateLocal.Value.Month))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build one entry per month in the window, newest first.
        var months = new List<RunMonthStatsDto>();
        var cursor = new DateOnly(today.Year, today.Month, 1);

        while (cursor >= localWindowStart)
        {
            var key = (cursor.Year, cursor.Month);
            var monthActivities = byMonth.TryGetValue(key, out var acts) ? acts : [];
            months.Add(BuildMonthStats(cursor.Year, cursor.Month, monthActivities));
            cursor = cursor.AddMonths(-1);
        }

        // Compute YTD from the already-filtered activity list.
        var ytdActivities = runActivities
            .Where(a => a.StartDateLocal.HasValue && a.StartDateLocal.Value.Year == today.Year)
            .ToList();

        var ytd = new RunYtdSummaryDto
        {
            Year = today.Year,
            TotalRuns = ytdActivities.Count,
            TotalDistanceMiles = ytdActivities.Sum(a => a.Distance) / MetresPerMile
        };

        return new RunStatsResponse
        {
            YtdSummary = ytd,
            Months = months.ToArray()
        };
    }

    /// <summary>
    /// Computes aggregated statistics for a single calendar month.
    /// </summary>
    /// <param name="year">The calendar year.</param>
    /// <param name="month">The month number (1–12).</param>
    /// <param name="activities">The run activities that occurred in this month.</param>
    /// <returns>A populated <see cref="RunMonthStatsDto"/>.</returns>
    private static RunMonthStatsDto BuildMonthStats(int year, int month, List<StravaActivity> activities)
    {
        if (activities.Count == 0)
        {
            return new RunMonthStatsDto { Year = year, Month = month };
        }

        var longestRun = activities.MaxBy(a => a.Distance);
        var qualifiedForFastest = activities
            .Where(a => a.Distance >= MinFastestRunMetres && ComputePaceSecondsPerMile(a) < double.MaxValue)
            .ToList();
        var fastestRun = qualifiedForFastest.Count > 0
            ? qualifiedForFastest.MinBy(a => ComputePaceSecondsPerMile(a))
            : null;

        return new RunMonthStatsDto
        {
            Year = year,
            Month = month,
            TotalRuns = activities.Count,
            TotalMovingTimeSeconds = activities.Sum(a => a.MovingTime),
            TotalDistanceMiles = activities.Sum(a => a.Distance) / MetresPerMile,
            TotalElevationFeet = activities.Sum(a => a.TotalElevationGain) * FeetPerMetre,
            LongestRunDistanceMiles = longestRun is not null ? longestRun.Distance / MetresPerMile : null,
            LongestRunPaceSecondsPerMile = longestRun is not null ? ComputePaceSecondsPerMile(longestRun) : null,
            FastestRunPaceSecondsPerMile = fastestRun is not null ? ComputePaceSecondsPerMile(fastestRun) : null,
            FastestRunDistanceMiles = fastestRun is not null ? fastestRun.Distance / MetresPerMile : null
        };
    }

    /// <summary>
    /// Computes pace in seconds per mile for the given activity.
    /// Uses <see cref="StravaActivity.AverageSpeed"/> when available; falls back to
    /// deriving speed from distance and moving time.
    /// </summary>
    /// <param name="activity">The activity to compute pace for.</param>
    /// <returns>Pace in seconds per mile, or <see cref="double.MaxValue"/> when speed cannot be determined.</returns>
    private static double ComputePaceSecondsPerMile(StravaActivity activity)
    {
        double speedMs;

        if (activity.AverageSpeed.HasValue && activity.AverageSpeed.Value > 0)
        {
            speedMs = activity.AverageSpeed.Value;
        }
        else if (activity.MovingTime > 0 && activity.Distance > 0)
        {
            speedMs = activity.Distance / activity.MovingTime;
        }
        else
        {
            return double.MaxValue;
        }

        // pace (sec/mi) = metres_per_mile / speed_in_m_per_s
        return MetresPerMile / speedMs;
    }
}
