namespace RookRun.Strava.Repositories;

/// <summary>
/// Represents filters and paging options for listing Strava activities from persistence.
/// </summary>
public sealed record ListStravaActivitiesQuery
{
    /// <summary>
    /// Gets the default page size for listing queries.
    /// </summary>
    public const int DefaultPageSize = 100;

    /// <summary>
    /// Gets the maximum allowed page size for listing queries.
    /// </summary>
    public const int MaxPageSize = 1000;

    /// <summary>
    /// Gets or sets the 1-based page number.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// Gets or sets the inclusive UTC lower bound for activity start date.
    /// </summary>
    public DateTimeOffset? StartDateUtc { get; init; }

    /// <summary>
    /// Gets or sets the inclusive UTC upper bound for activity start date.
    /// </summary>
    public DateTimeOffset? EndDateUtc { get; init; }

    /// <summary>
    /// Gets or sets an optional activity type filter.
    /// </summary>
    public string? ActivityType { get; init; }

    /// <summary>
    /// Gets or sets the sort direction for activity start date.
    /// </summary>
    public StravaActivitiesSortDirection SortDirection { get; init; } = StravaActivitiesSortDirection.Desc;
}