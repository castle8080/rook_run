using RookRun.Contracts.Strava;

namespace RookRun.Web.Pages;

/// <summary>
/// Builds CSV payloads for activities data exports.
/// </summary>
internal static class ActivitiesCsvExporter
{
    /// <summary>
    /// Builds CSV content from the supplied activity collection.
    /// </summary>
    /// <param name="items">The activities to serialize to CSV.</param>
    /// <returns>The CSV text payload.</returns>
    public static string BuildCsv(IReadOnlyList<StravaActivityDto> items)
    {
        var lines = new List<string>(items.Count + 1)
        {
            "StartDateUtc,StartDateLocal,Name,Type,ElapsedTime,DistanceMiles,PaceMinPerMile,ElevationFeet,AverageHeartrate,MaxHeartrate"
        };

        foreach (var item in items)
        {
            var row = string.Join(",",
                EscapeCsv(ActivitiesFormatting.FormatUtcDate(item.StartDate)),
                EscapeCsv(ActivitiesFormatting.FormatLocalDate(item.StartDateLocal)),
                EscapeCsv(item.Name ?? string.Empty),
                EscapeCsv(ActivitiesFormatting.ResolveActivityType(item)),
                EscapeCsv(ActivitiesFormatting.FormatElapsedTime(item.ElapsedTime)),
                EscapeCsv(ActivitiesFormatting.FormatMiles(item.Distance)),
                EscapeCsv(ActivitiesFormatting.FormatPaceMinutesPerMile(item.MovingTime, item.Distance)),
                EscapeCsv(ActivitiesFormatting.FormatElevationFeet(item.TotalElevationGain)),
                EscapeCsv(ActivitiesFormatting.FormatHeartRate(item.AverageHeartrate)),
                EscapeCsv(ActivitiesFormatting.FormatHeartRate(item.MaxHeartrate)));

            lines.Add(row);
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Escapes a value for safe inclusion in a CSV field.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped CSV field.</returns>
    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}";
        }

        return value;
    }
}
