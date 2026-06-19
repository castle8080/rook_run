using System.Globalization;
using RookRun.Contracts.Strava;

namespace RookRun.Web.Pages;

/// <summary>
/// Provides formatting and display helpers for activities page components.
/// </summary>
internal static class ActivitiesFormatting
{
    private const double MetersPerMile = 1609.344;
    private const double FeetPerMeter = 3.28084;

    /// <summary>
    /// Formats a UTC date/time string for display.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>A formatted UTC timestamp, or a placeholder.</returns>
    public static string FormatUtcDate(DateTimeOffset? value) => value is null
        ? "-"
        : value.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a local date/time string for display.
    /// </summary>
    /// <param name="value">The local date/time value to format.</param>
    /// <returns>A formatted local timestamp, or a placeholder.</returns>
    public static string FormatLocalDate(DateTimeOffset? value) => value is null
        ? "-"
        : value.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats elapsed time in hh:mm:ss.
    /// </summary>
    /// <param name="elapsedTimeSeconds">Elapsed time in seconds.</param>
    /// <returns>The formatted elapsed time, or a placeholder.</returns>
    public static string FormatElapsedTime(int elapsedTimeSeconds)
    {
        if (elapsedTimeSeconds <= 0)
        {
            return "-";
        }

        var elapsed = TimeSpan.FromSeconds(elapsedTimeSeconds);
        return elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"00:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    /// <summary>
    /// Formats distance in miles from meters.
    /// </summary>
    /// <param name="distanceMeters">Distance in meters.</param>
    /// <returns>The formatted miles value.</returns>
    public static string FormatMiles(double distanceMeters)
    {
        if (distanceMeters <= 0)
        {
            return "-";
        }

        var miles = distanceMeters / MetersPerMile;
        return miles.ToString("F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats pace as minutes per mile using mm:ss format.
    /// </summary>
    /// <param name="movingTimeSeconds">Moving time in seconds.</param>
    /// <param name="distanceMeters">Distance in meters.</param>
    /// <returns>The formatted pace string, or a placeholder.</returns>
    public static string FormatPaceMinutesPerMile(int movingTimeSeconds, double distanceMeters)
    {
        if (movingTimeSeconds <= 0 || distanceMeters <= 0)
        {
            return "-";
        }

        var miles = distanceMeters / MetersPerMile;
        if (miles <= 0)
        {
            return "-";
        }

        var secondsPerMile = movingTimeSeconds / miles;
        var pace = TimeSpan.FromSeconds(secondsPerMile);
        var totalMinutes = (int)pace.TotalMinutes;
        return $"{totalMinutes:00}:{pace.Seconds:00}";
    }

    /// <summary>
    /// Formats elevation gain in feet from meters.
    /// </summary>
    /// <param name="elevationMeters">Elevation gain in meters.</param>
    /// <returns>The formatted elevation value.</returns>
    public static string FormatElevationFeet(double elevationMeters)
    {
        var feet = elevationMeters * FeetPerMeter;
        return feet.ToString("F0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats heart-rate values for display.
    /// </summary>
    /// <param name="heartRate">Heart rate value in beats per minute.</param>
    /// <returns>The formatted heart-rate string.</returns>
    public static string FormatHeartRate(double? heartRate) => heartRate is null
        ? "-"
        : heartRate.Value.ToString("F0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Resolves an activity type string using Type first, then SportType.
    /// </summary>
    /// <param name="activity">The activity to inspect.</param>
    /// <returns>A display-friendly activity type.</returns>
    public static string ResolveActivityType(StravaActivityDto activity)
    {
        if (!string.IsNullOrWhiteSpace(activity.Type))
        {
            return activity.Type;
        }

        if (!string.IsNullOrWhiteSpace(activity.SportType))
        {
            return activity.SportType;
        }

        return "-";
    }
}
