namespace RookRun.Contracts.Strava;

/// <summary>
/// Represents year-to-date running totals for the current calendar year.
/// </summary>
public sealed class RunYtdSummaryDto
{
    /// <summary>
    /// Gets or sets the calendar year this summary covers.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Gets or sets the total number of run activities in the year so far.
    /// </summary>
    public int TotalRuns { get; set; }

    /// <summary>
    /// Gets or sets the total distance in miles for the year so far.
    /// </summary>
    public double TotalDistanceMiles { get; set; }
}
