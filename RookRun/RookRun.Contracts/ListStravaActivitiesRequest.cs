namespace RookRun.Contracts;

/// <summary>
/// Represents query parameters for listing Strava activities.
/// </summary>
public sealed class ListStravaActivitiesRequest
{
    /// <summary>
    /// Gets or sets the 1-based page number.
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// Gets or sets the requested page size.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// Gets or sets the inclusive UTC lower bound for activity start date.
    /// </summary>
    public DateTimeOffset? StartDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the inclusive UTC upper bound for activity start date.
    /// </summary>
    public DateTimeOffset? EndDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the activity type filter.
    /// </summary>
    public string? ActivityType { get; set; }

    /// <summary>
    /// Gets or sets the sort direction. Defaults to <see cref="SortDirection.Desc"/>.
    /// </summary>
    public SortDirection? SortDirection { get; set; }
}
