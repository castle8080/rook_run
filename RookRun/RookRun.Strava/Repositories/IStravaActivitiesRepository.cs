using RookRun.Strava.Models;

namespace RookRun.Strava.Repositories;

/// <summary>
/// Provides persistence operations for saving, searching, and deleting Strava activities.
/// </summary>
public interface IStravaActivitiesRepository
{
    /// <summary>
    /// Lists activities using the supplied filters, ordering, and paging options.
    /// </summary>
    /// <param name="query">The list query options.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A paged list result containing matching activities.</returns>
    Task<ListStravaActivitiesResult> ListAsync(ListStravaActivitiesQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists activity IDs whose dates fall within the inclusive range.
    /// </summary>
    /// <param name="startInclusive">The inclusive lower bound of the search range.</param>
    /// <param name="endInclusive">The inclusive upper bound of the search range.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>Distinct activity IDs matching the date range.</returns>
    Task<IReadOnlyList<long>> ListActivityIdsAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endInclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the supplied activities to the backing store.
    /// </summary>
    /// <param name="activities">The activities to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task SaveAllAsync(IReadOnlyList<StravaActivity> activities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for activities whose dates fall within the inclusive range.
    /// </summary>
    /// <param name="startInclusive">The inclusive lower bound of the search range.</param>
    /// <param name="endInclusive">The inclusive upper bound of the search range.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching activities.</returns>
    Task<IReadOnlyList<StravaActivity>> SearchAsync(DateTimeOffset startInclusive, DateTimeOffset endInclusive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the supplied activities from the backing store.
    /// </summary>
    /// <param name="activities">The activities to delete.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task DeleteAllAsync(IReadOnlyList<StravaActivity> activities, CancellationToken cancellationToken = default);
}