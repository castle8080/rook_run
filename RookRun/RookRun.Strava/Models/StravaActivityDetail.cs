using System.Text.Json;
using System.Text.Json.Serialization;

namespace RookRun.Strava.Models;

/// <summary>
/// Represents a detailed activity from Strava API with comprehensive information
/// including segment efforts, laps, splits, and associated metadata.
/// Retrieved from GET /activities/{id} endpoint.
/// </summary>
public sealed record StravaActivityDetail
{
    /// <summary>
    /// The unique identifier of the activity.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>
    /// The name of the activity.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The activity's distance, in meters.
    /// </summary>
    [JsonPropertyName("distance")]
    public double Distance { get; init; }

    /// <summary>
    /// The activity's moving time, in seconds.
    /// </summary>
    [JsonPropertyName("moving_time")]
    public int MovingTime { get; init; }

    /// <summary>
    /// The activity's elapsed time, in seconds.
    /// </summary>
    [JsonPropertyName("elapsed_time")]
    public int ElapsedTime { get; init; }

    /// <summary>
    /// The activity's total elevation gain, in meters.
    /// </summary>
    [JsonPropertyName("total_elevation_gain")]
    public double TotalElevationGain { get; init; }

    /// <summary>
    /// The activity's type (e.g., Run, Ride).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// The activity's sport type (e.g., MountainBikeRide, Run).
    /// </summary>
    [JsonPropertyName("sport_type")]
    public string? SportType { get; init; }

    /// <summary>
    /// The time at which the activity was started, in UTC.
    /// </summary>
    [JsonPropertyName("start_date")]
    public DateTimeOffset? StartDate { get; init; }

    /// <summary>
    /// The time at which the activity was started, in the local timezone.
    /// </summary>
    [JsonPropertyName("start_date_local")]
    public DateTimeOffset? StartDateLocal { get; init; }

    /// <summary>
    /// The activity's timezone.
    /// </summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }

    /// <summary>
    /// The activity's start lat/lng coordinates, as [lat, lng].
    /// </summary>
    [JsonPropertyName("start_latlng")]
    public double[]? StartLatLng { get; init; }

    /// <summary>
    /// The activity's end lat/lng coordinates, as [lat, lng].
    /// </summary>
    [JsonPropertyName("end_latlng")]
    public double[]? EndLatLng { get; init; }

    /// <summary>
    /// The number of achievements gained during this activity.
    /// </summary>
    [JsonPropertyName("achievement_count")]
    public int AchievementCount { get; init; }

    /// <summary>
    /// The number of kudos given for this activity.
    /// </summary>
    [JsonPropertyName("kudos_count")]
    public int KudosCount { get; init; }

    /// <summary>
    /// The number of comments for this activity.
    /// </summary>
    [JsonPropertyName("comment_count")]
    public int CommentCount { get; init; }

    /// <summary>
    /// The number of athletes taking part in a group activity.
    /// </summary>
    [JsonPropertyName("athlete_count")]
    public int AthleteCount { get; init; }

    /// <summary>
    /// The number of photos for this activity.
    /// </summary>
    [JsonPropertyName("photo_count")]
    public int PhotoCount { get; init; }

    /// <summary>
    /// The number of Instagram and Strava photos for this activity.
    /// </summary>
    [JsonPropertyName("total_photo_count")]
    public int TotalPhotoCount { get; init; }

    /// <summary>
    /// The activity's average speed, in meters per second.
    /// </summary>
    [JsonPropertyName("average_speed")]
    public double? AverageSpeed { get; init; }

    /// <summary>
    /// The activity's max speed, in meters per second.
    /// </summary>
    [JsonPropertyName("max_speed")]
    public double? MaxSpeed { get; init; }

    /// <summary>
    /// The activity's average cadence, in rotations per minute.
    /// </summary>
    [JsonPropertyName("average_cadence")]
    public double? AverageCadence { get; init; }

    /// <summary>
    /// The activity's average watts.
    /// </summary>
    [JsonPropertyName("average_watts")]
    public double? AverageWatts { get; init; }

    /// <summary>
    /// Whether the watts are from a power meter.
    /// </summary>
    [JsonPropertyName("device_watts")]
    public bool DeviceWatts { get; init; }

    /// <summary>
    /// The activity's max watts.
    /// </summary>
    [JsonPropertyName("max_watts")]
    public int? MaxWatts { get; init; }

    /// <summary>
    /// The activity's average heart rate.
    /// </summary>
    [JsonPropertyName("average_heartrate")]
    public double? AverageHeartrate { get; init; }

    /// <summary>
    /// The activity's max heart rate.
    /// </summary>
    [JsonPropertyName("max_heartrate")]
    public double? MaxHeartrate { get; init; }

    /// <summary>
    /// The description of the activity.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Whether this activity was recorded on a trainer.
    /// </summary>
    [JsonPropertyName("trainer")]
    public bool Trainer { get; init; }

    /// <summary>
    /// Whether this activity is a commute.
    /// </summary>
    [JsonPropertyName("commute")]
    public bool Commute { get; init; }

    /// <summary>
    /// Whether this activity was created manually.
    /// </summary>
    [JsonPropertyName("manual")]
    public bool Manual { get; init; }

    /// <summary>
    /// Whether this activity is private.
    /// </summary>
    [JsonPropertyName("private")]
    public bool Private { get; init; }

    /// <summary>
    /// Whether this activity is flagged.
    /// </summary>
    [JsonPropertyName("flagged")]
    public bool Flagged { get; init; }

    /// <summary>
    /// The id of the gear for the activity.
    /// </summary>
    [JsonPropertyName("gear_id")]
    public string? GearId { get; init; }

    /// <summary>
    /// The activity's workout type.
    /// </summary>
    [JsonPropertyName("workout_type")]
    public int? WorkoutType { get; init; }

    /// <summary>
    /// The number of calories consumed during this activity.
    /// </summary>
    [JsonPropertyName("calories")]
    public double? Calories { get; init; }

    /// <summary>
    /// The token used to embed a Strava activity.
    /// </summary>
    [JsonPropertyName("embed_token")]
    public string? EmbedToken { get; init; }

    /// <summary>
    /// Segment efforts completed during this activity.
    /// </summary>
    [JsonPropertyName("segment_efforts")]
    public object[]? SegmentEfforts { get; init; }

    /// <summary>
    /// The splits of this activity in metric units (for runs).
    /// </summary>
    [JsonPropertyName("splits_metric")]
    public object[]? SplitsMetric { get; init; }

    /// <summary>
    /// The splits of this activity in imperial units (for runs).
    /// </summary>
    [JsonPropertyName("splits_standard")]
    public object[]? SplitsStandard { get; init; }

    /// <summary>
    /// The laps of this activity.
    /// </summary>
    [JsonPropertyName("laps")]
    public object[]? Laps { get; init; }

    /// <summary>
    /// Best efforts of this activity (best segment efforts).
    /// </summary>
    [JsonPropertyName("best_efforts")]
    public object[]? BestEfforts { get; init; }

    /// <summary>
    /// Photos associated with this activity, including primary photo and photo array.
    /// </summary>
    [JsonPropertyName("photos")]
    public object? Photos { get; init; }

    /// <summary>
    /// Gear associated with this activity.
    /// </summary>
    [JsonPropertyName("gear")]
    public object? Gear { get; init; }

    /// <summary>
    /// Unmapped fields from Strava API response, captured for extensibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> UnmappedFields { get; init; } = [];
}
