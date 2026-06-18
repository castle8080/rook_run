using System.Text.Json;
using System.Text.Json.Serialization;

namespace RookRun.Strava.Models;

/// <summary>
/// Represents metadata for an image associated with a Strava activity.
/// Image data is extracted from the activity's photos object.
/// </summary>
public sealed record StravaActivityImage
{
    /// <summary>
    /// The activity ID this image belongs to.
    /// </summary>
    [JsonPropertyName("activity_id")]
    public long ActivityId { get; init; }

    /// <summary>
    /// The unique identifier of the image (typically unique_id from Strava photos).
    /// </summary>
    [JsonPropertyName("image_id")]
    public string ImageId { get; init; } = string.Empty;

    /// <summary>
    /// The URL to download the image from (typically a CloudFront CDN URL).
    /// </summary>
    [JsonPropertyName("image_url")]
    public string ImageUrl { get; init; } = string.Empty;

    /// <summary>
    /// File extension for this image (e.g., "jpg", "png").
    /// </summary>
    [JsonPropertyName("extension")]
    public string Extension { get; init; } = string.Empty;

    /// <summary>
    /// Optional size variant if available (e.g., "100", "600" from different resolution URLs).
    /// </summary>
    [JsonPropertyName("size_variant")]
    public string? SizeVariant { get; init; }

    /// <summary>
    /// The source of the photo (1 for Instagram, 2 for Strava native, etc.).
    /// </summary>
    [JsonPropertyName("source")]
    public int? Source { get; init; }

    /// <summary>
    /// Unmapped fields from extraction, captured for extensibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> UnmappedFields { get; init; } = [];
}
