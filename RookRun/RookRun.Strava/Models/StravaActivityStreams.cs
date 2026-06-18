using System.Text.Json;
using System.Text.Json.Serialization;

namespace RookRun.Strava.Models;

/// <summary>
/// Represents the cached stream set for a single Strava activity.
/// </summary>
public sealed record StravaActivityStreams
{
    /// <summary>
    /// Gets the Strava activity identifier.
    /// </summary>
    [JsonPropertyName("activity_id")]
    public long ActivityId { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when this stream document was fetched.
    /// </summary>
    [JsonPropertyName("fetched_utc")]
    public DateTimeOffset FetchedUtc { get; init; }

    /// <summary>
    /// Gets the stream keys requested from Strava for this fetch.
    /// </summary>
    [JsonPropertyName("requested_keys")]
    public IReadOnlyList<string> RequestedKeys { get; init; } = [];

    /// <summary>
    /// Gets streams keyed by stream type (for example, "time" or "heartrate").
    /// </summary>
    [JsonPropertyName("streams")]
    public Dictionary<string, StravaStreamData> Streams { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets unmapped JSON fields for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> UnmappedFields { get; init; } = [];
}
