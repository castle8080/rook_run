using System.Text.Json;

namespace RookRun.Contracts.Strava;

/// <summary>
/// Represents a Strava activity returned by API endpoints.
/// </summary>
public sealed class StravaActivityDto
{
    /// <summary>
    /// Gets or sets the Strava activity identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the activity name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the activity type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the Strava sport type.
    /// </summary>
    public string? SportType { get; set; }

    /// <summary>
    /// Gets or sets the UTC start date and time.
    /// </summary>
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the local start date and time.
    /// </summary>
    public DateTimeOffset? StartDateLocal { get; set; }

    /// <summary>
    /// Gets or sets the distance in meters.
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Gets or sets moving time in seconds.
    /// </summary>
    public int MovingTime { get; set; }

    /// <summary>
    /// Gets or sets elapsed time in seconds.
    /// </summary>
    public int ElapsedTime { get; set; }

    /// <summary>
    /// Gets or sets total elevation gain in meters.
    /// </summary>
    public double TotalElevationGain { get; set; }

    /// <summary>
    /// Gets or sets average speed in meters per second.
    /// </summary>
    public double? AverageSpeed { get; set; }

    /// <summary>
    /// Gets or sets maximum speed in meters per second.
    /// </summary>
    public double? MaxSpeed { get; set; }

    /// <summary>
    /// Gets or sets average heart rate in beats per minute.
    /// </summary>
    public double? AverageHeartrate { get; set; }

    /// <summary>
    /// Gets or sets maximum heart rate in beats per minute.
    /// </summary>
    public double? MaxHeartrate { get; set; }

    /// <summary>
    /// Gets or sets the starting latitude/longitude pair.
    /// </summary>
    public double[]? StartLatLng { get; set; }

    /// <summary>
    /// Gets or sets the ending latitude/longitude pair.
    /// </summary>
    public double[]? EndLatLng { get; set; }

    /// <summary>
    /// Gets or sets additional untyped activity properties.
    /// </summary>
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = [];
}
