using System.Text.Json;
using System.Text.Json.Serialization;

namespace RookRun.Strava.Models;

public sealed record StravaActivity
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("sport_type")]
    public string? SportType { get; init; }

    [JsonPropertyName("start_date")]
    public DateTimeOffset? StartDate { get; init; }

    [JsonPropertyName("start_date_local")]
    public DateTimeOffset? StartDateLocal { get; init; }

    [JsonPropertyName("distance")]
    public double Distance { get; init; }

    [JsonPropertyName("moving_time")]
    public int MovingTime { get; init; }

    [JsonPropertyName("elapsed_time")]
    public int ElapsedTime { get; init; }

    [JsonPropertyName("total_elevation_gain")]
    public double TotalElevationGain { get; init; }

    [JsonPropertyName("average_speed")]
    public double? AverageSpeed { get; init; }

    [JsonPropertyName("max_speed")]
    public double? MaxSpeed { get; init; }

    [JsonPropertyName("average_heartrate")]
    public double? AverageHeartrate { get; init; }

    [JsonPropertyName("max_heartrate")]
    public double? MaxHeartrate { get; init; }

    [JsonPropertyName("start_latlng")]
    public double[]? StartLatLng { get; init; }

    [JsonPropertyName("end_latlng")]
    public double[]? EndLatLng { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; init; } = [];
}