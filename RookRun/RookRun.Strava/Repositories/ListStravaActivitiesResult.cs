using RookRun.Strava.Models;

namespace RookRun.Strava.Repositories;

/// <summary>
/// Represents a paged result of Strava activity repository queries.
/// </summary>
public sealed record ListStravaActivitiesResult
{
    /// <summary>
    /// Gets or sets the current page.
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether there is another page after this one.
    /// </summary>
    public bool HasNextPage { get; init; }

    /// <summary>
    /// Gets or sets the activities in this page.
    /// </summary>
    public IReadOnlyList<StravaActivity> Items { get; init; } = [];
}