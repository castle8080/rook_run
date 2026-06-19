using System.Text.Json;
using System.Text.Json.Serialization;

namespace RookRun.Strava.Models;

/// <summary>
/// Represents the cached activity-to-image-id index used to avoid rediscovering photo IDs.
/// </summary>
public sealed record StravaActivityImageIdIndex
{
    /// <summary>
    /// Gets the indexed image IDs keyed by activity ID string.
    /// </summary>
    [JsonPropertyName("items")]
    public Dictionary<string, List<string>> Items { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the UTC timestamp when the index was last updated.
    /// </summary>
    [JsonPropertyName("updated_utc")]
    public DateTimeOffset UpdatedUtc { get; init; }

    /// <summary>
    /// Gets any unmapped fields preserved during serialization.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> UnmappedFields { get; init; } = [];
}