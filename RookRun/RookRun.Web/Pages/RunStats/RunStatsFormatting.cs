namespace RookRun.Web.Pages;

/// <summary>
/// Provides formatting helpers shared by run stats page components.
/// </summary>
internal static class RunStatsFormatting
{
    /// <summary>
    /// Formats a total seconds value as a human-readable duration.
    /// Examples: 0 -> "0m", 45 -> "45m", 3600 -> "1h 00m", 9720 -> "2h 42m".
    /// </summary>
    /// <param name="totalSeconds">The duration in seconds.</param>
    /// <returns>A formatted duration string.</returns>
    public static string FormatTime(int totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return "0m";
        }

        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;

        return hours > 0
            ? $"{hours}h {minutes:D2}m"
            : $"{minutes}m";
    }

    /// <summary>
    /// Formats pace in seconds per mile as a min:sec/mi string.
    /// Example: 542.4 -> "9:02/mi".
    /// </summary>
    /// <param name="secsPerMile">The pace in seconds per mile.</param>
    /// <returns>A formatted pace string.</returns>
    public static string FormatPace(double secsPerMile)
    {
        var totalSecs = (int)Math.Round(secsPerMile);
        var mins = totalSecs / 60;
        var secs = totalSecs % 60;
        return $"{mins}:{secs:D2}/mi";
    }

    /// <summary>
    /// Formats a distance in miles to one decimal place.
    /// Example: 62.3 -> "62.3 mi".
    /// </summary>
    /// <param name="miles">The distance in miles.</param>
    /// <returns>A formatted distance string.</returns>
    public static string FormatDist(double miles) => $"{miles:F1} mi";

    /// <summary>
    /// Formats elevation in feet as a whole number with thousands separator.
    /// Example: 3420.7 -> "3,421 ft".
    /// </summary>
    /// <param name="feet">The elevation in feet.</param>
    /// <returns>A formatted elevation string.</returns>
    public static string FormatElev(double feet) => $"{(int)Math.Round(feet):N0} ft";

    /// <summary>
    /// Formats a year and month as a long label for use in mobile cards.
    /// Example: (2026, 6) -> "June 2026".
    /// </summary>
    /// <param name="year">The four-digit year.</param>
    /// <param name="month">The month number (1-12).</param>
    /// <returns>A formatted month label.</returns>
    public static string FormatMonthLong(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMMM yyyy");

    /// <summary>
    /// Formats a year and month as a short label for use in the desktop table header.
    /// Example: (2026, 6) -> "Jun '26".
    /// </summary>
    /// <param name="year">The four-digit year.</param>
    /// <param name="month">The month number (1-12).</param>
    /// <returns>A formatted short month label.</returns>
    public static string FormatMonthShort(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMM ''yy");
}
