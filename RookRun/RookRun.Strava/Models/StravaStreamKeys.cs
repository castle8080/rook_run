namespace RookRun.Strava.Models;

/// <summary>
/// Defines Strava activity stream key constants.
/// </summary>
public static class StravaStreamKeys
{
    /// <summary>
    /// Time stream key.
    /// </summary>
    public const string Time = "time";

    /// <summary>
    /// Distance stream key.
    /// </summary>
    public const string Distance = "distance";

    /// <summary>
    /// Altitude stream key.
    /// </summary>
    public const string Altitude = "altitude";

    /// <summary>
    /// Heart rate stream key.
    /// </summary>
    public const string Heartrate = "heartrate";

    /// <summary>
    /// Cadence stream key.
    /// </summary>
    public const string Cadence = "cadence";

    /// <summary>
    /// Power stream key.
    /// </summary>
    public const string Watts = "watts";

    /// <summary>
    /// Temperature stream key.
    /// </summary>
    public const string Temp = "temp";

    /// <summary>
    /// Moving stream key.
    /// </summary>
    public const string Moving = "moving";

    /// <summary>
    /// Smoothed grade stream key.
    /// </summary>
    public const string GradeSmooth = "grade_smooth";

    /// <summary>
    /// Smoothed velocity stream key.
    /// </summary>
    public const string VelocitySmooth = "velocity_smooth";

    /// <summary>
    /// Latitude/longitude stream key.
    /// </summary>
    public const string LatLng = "latlng";

    /// <summary>
    /// Gets all supported activity stream keys for phase 1.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultPhase1 =
    [
        Time,
        Distance,
        Altitude,
        Heartrate,
        Cadence,
        Watts,
        Temp,
        Moving,
        GradeSmooth,
        VelocitySmooth,
        LatLng
    ];
}
