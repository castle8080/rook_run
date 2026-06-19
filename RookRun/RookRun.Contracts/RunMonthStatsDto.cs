namespace RookRun.Contracts.Strava;

/// <summary>
/// Represents aggregated running statistics for a single calendar month.
/// </summary>
public sealed class RunMonthStatsDto
{
    /// <summary>
    /// Gets or sets the four-digit calendar year for this month.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Gets or sets the month number (1–12) for this entry.
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Gets or sets the total number of run activities in this month.
    /// </summary>
    public int TotalRuns { get; set; }

    /// <summary>
    /// Gets or sets the total moving time in seconds for this month.
    /// </summary>
    public int TotalMovingTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the total distance in miles for this month.
    /// </summary>
    public double TotalDistanceMiles { get; set; }

    /// <summary>
    /// Gets or sets the total elevation gain in feet for this month.
    /// </summary>
    public double TotalElevationFeet { get; set; }

    /// <summary>
    /// Gets or sets the distance in miles for the longest run in this month,
    /// or <c>null</c> if there were no run activities.
    /// </summary>
    public double? LongestRunDistanceMiles { get; set; }

    /// <summary>
    /// Gets or sets the pace in seconds per mile for the longest run in this month,
    /// or <c>null</c> if there were no run activities.
    /// </summary>
    public double? LongestRunPaceSecondsPerMile { get; set; }

    /// <summary>
    /// Gets or sets the best (fastest) average pace in seconds per mile for any run
    /// in this month that is at least 1 mile long, or <c>null</c> if there were no
    /// qualifying runs.
    /// </summary>
    public double? FastestRunPaceSecondsPerMile { get; set; }

    /// <summary>
    /// Gets or sets the distance in miles of the fastest run in this month,
    /// or <c>null</c> if there were no qualifying runs.
    /// </summary>
    public double? FastestRunDistanceMiles { get; set; }
}
