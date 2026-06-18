using System.Text.Json;
using System.Text.Json.Serialization;

namespace RookRun.Strava.Models;

/// <summary>
/// Represents one Strava stream and its metadata.
/// </summary>
public sealed record StravaStreamData
{
    /// <summary>
    /// Gets the stream type (for example, "time", "distance", or "heartrate").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets the base series used when Strava downsampled this stream.
    /// </summary>
    [JsonPropertyName("series_type")]
    public string? SeriesType { get; init; }

    /// <summary>
    /// Gets the stream resolution returned by Strava.
    /// </summary>
    [JsonPropertyName("resolution")]
    public string? Resolution { get; init; }

    /// <summary>
    /// Gets the original number of points in this stream.
    /// </summary>
    [JsonPropertyName("original_size")]
    public int? OriginalSize { get; init; }

    /// <summary>
    /// Gets raw stream data from Strava. The JSON type varies by stream key.
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement Data { get; init; }

    /// <summary>
    /// Gets unmapped JSON fields for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> UnmappedFields { get; init; } = [];
}
