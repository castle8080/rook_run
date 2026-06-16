namespace RookRun.Contracts;

/// <summary>
/// Represents a paged response for Strava activities.
/// </summary>
public sealed class ListStravaActivitiesResponse
{
    /// <summary>
    /// Gets or sets the current page.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage { get; set; }

    /// <summary>
    /// Gets or sets the activities in the current page.
    /// </summary>
    public IReadOnlyList<StravaActivityDto> Items { get; set; } = [];
}
