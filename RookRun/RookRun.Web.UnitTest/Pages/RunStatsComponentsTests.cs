using Bunit;
using RookRun.Contracts.Strava;
using RookRun.Web.Pages;

namespace RookRun.Web.UnitTest.Pages;

/// <summary>
/// Unit tests for Run Stats UI components.
/// </summary>
public sealed class RunStatsComponentsTests : TestContext
{
    /// <summary>
    /// Verifies the YTD card renders year, run count, and formatted distance.
    /// </summary>
    [Fact]
    public void RunStatsYtdCard_RendersSummaryValues()
    {
        var summary = new RunYtdSummaryDto
        {
            Year = 2026,
            TotalRuns = 48,
            TotalDistanceMiles = 312.44
        };

        var cut = RenderComponent<RunStatsYtdCard>(parameters =>
            parameters.Add(p => p.Summary, summary));

        Assert.Contains("2026 Year to Date", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("48", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("312.4 mi", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the desktop table renders month rows, formatted metrics, and pace details.
    /// </summary>
    [Fact]
    public void RunStatsDesktopTable_RendersFormattedRows()
    {
        var months = new[]
        {
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
        };

        var cut = RenderComponent<RunStatsDesktopTable>(parameters =>
            parameters.Add(p => p.Months, months));

        var rowText = cut.Find("tbody tr").TextContent;

        Assert.Contains("Jun", rowText, StringComparison.Ordinal);
        Assert.Contains("8", rowText, StringComparison.Ordinal);
        Assert.Contains("9h 12m", rowText, StringComparison.Ordinal);
        Assert.Contains("62.3 mi", rowText, StringComparison.Ordinal);
        Assert.Contains("14.2 mi", rowText, StringComparison.Ordinal);
        Assert.Contains("@ 9:12/mi", rowText, StringComparison.Ordinal);
        Assert.Contains("9:02/mi", rowText, StringComparison.Ordinal);
        Assert.Contains("(6.2 mi)", rowText, StringComparison.Ordinal);
        Assert.Contains("3,420 ft", rowText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the desktop table renders fallback placeholders when longest/fastest data is unavailable.
    /// </summary>
    [Fact]
    public void RunStatsDesktopTable_RendersFallbackWhenOptionalValuesMissing()
    {
        var months = new[]
        {
            new RunMonthStatsDto
            {
                Year = 2026,
                Month = 5,
                TotalRuns = 0,
                TotalMovingTimeSeconds = 0,
                TotalDistanceMiles = 0,
                TotalElevationFeet = 0,
                LongestRunDistanceMiles = null,
                LongestRunPaceSecondsPerMile = null,
                FastestRunPaceSecondsPerMile = null,
                FastestRunDistanceMiles = null
            }
        };

        var cut = RenderComponent<RunStatsDesktopTable>(parameters =>
            parameters.Add(p => p.Months, months));

        var fallbackCells = cut.FindAll("span.text-muted");
        Assert.True(fallbackCells.Count >= 2);
        Assert.Contains(fallbackCells, node => node.TextContent.Trim() == "-");
    }

    /// <summary>
    /// Verifies mobile cards render month label and key formatted fields.
    /// </summary>
    [Fact]
    public void RunStatsMobileCards_RendersMonthCardValues()
    {
        var months = new[]
        {
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
        };

        var cut = RenderComponent<RunStatsMobileCards>(parameters =>
            parameters.Add(p => p.Months, months));

        Assert.Contains("June 2026", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Runs", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("8", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("9h 12m", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("62.3 mi", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("3,420 ft", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("@ 9:12/mi", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("9:02/mi", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies desktop and mobile components safely render empty state when no
    /// month statistics are available.
    /// </summary>
    [Fact]
    public void RunStatsListComponents_RenderNoRowsOrCards_WhenMonthsIsEmpty()
    {
        var emptyMonths = Array.Empty<RunMonthStatsDto>();

        var desktop = RenderComponent<RunStatsDesktopTable>(parameters =>
            parameters.Add(p => p.Months, emptyMonths));
        var mobile = RenderComponent<RunStatsMobileCards>(parameters =>
            parameters.Add(p => p.Months, emptyMonths));

        Assert.Empty(desktop.FindAll("tbody tr"));
        Assert.Empty(mobile.FindAll(".stats-month-card"));
    }
}
