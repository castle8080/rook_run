namespace RookRun.Contracts.Strava;

/// <summary>
/// Represents the full response for the run stats endpoint, containing a YTD
/// summary and per-month aggregated statistics.
/// </summary>
public sealed class RunStatsResponse
{
    /// <summary>
    /// Gets or sets the year-to-date running summary for the current calendar year.
    /// </summary>
    public RunYtdSummaryDto YtdSummary { get; set; } = new();

    /// <summary>
    /// Gets or sets the month-by-month statistics, ordered from the most recent
    /// month descending to the start of the 13-month rolling window.
    /// </summary>
    public RunMonthStatsDto[] Months { get; set; } = [];
}
