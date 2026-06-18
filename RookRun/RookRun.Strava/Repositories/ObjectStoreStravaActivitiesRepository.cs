using Microsoft.Extensions.Options;
using RookRun.ObjectStore;
using RookRun.Strava.Models;
using RookRun.Strava.Options;

namespace RookRun.Strava.Repositories;

/// <summary>
/// Stores Strava activities in the configured object store using monthly partition files.
/// Activities are saved into a <c>strava_activities_yyyy-MM.json.br</c> file based on the UTC month from
/// <see cref="StravaActivity.StartDate"/>, then merged by activity id and written back in sorted order.
/// </summary>
public sealed class ObjectStoreStravaActivitiesRepository : IStravaActivitiesRepository
{
    private const string FileNamePrefix = "strava_activities_";
    private const string FileNameSuffix = ".json.br";

    private readonly IObjectStore objectStore;
    private readonly string prefix;

    public ObjectStoreStravaActivitiesRepository(
            IOptions<ObjectStoreStravaActivitiesRepositoryOptions> options,
            IObjectStore objectStore)
        : this(objectStore, options?.Value?.PathPrefix ?? string.Empty)
    {
    }

    public ObjectStoreStravaActivitiesRepository(IObjectStore objectStore, string prefix)
    {
        this.objectStore = objectStore ?? throw new ArgumentNullException(nameof(objectStore));
        this.prefix = NormalizePrefix(prefix);
    }

    /// <summary>
    /// Lists activities using optional date and type filters, ordered by start date, and paged.
    /// </summary>
    /// <param name="query">The list query options.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A paged list result containing matching activities.</returns>
    public async Task<ListStravaActivitiesResult> ListAsync(ListStravaActivitiesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateListQuery(query);

        var objectPaths = await objectStore.ListObjectsAsync(prefix, cancellationToken);
        var matches = new List<StravaActivity>();

        foreach (var objectPath in objectPaths)
        {
            if (!TryParsePartition(objectPath, out var partition) || !IsPartitionInRange(partition, query.StartDateUtc, query.EndDateUtc))
            {
                continue;
            }

            var activities = await LoadActivitiesAsync(objectPath, cancellationToken);
            matches.AddRange(activities.Where(activity => Matches(activity, query)));
        }

        var ordered = query.SortDirection == StravaActivitiesSortDirection.Asc
            ? OrderAscending(matches)
            : OrderDescending(matches);

        var skip = (query.Page - 1) * query.PageSize;
        var pagePlusOne = ordered
            .Skip(skip)
            .Take(query.PageSize + 1)
            .ToArray();
        var hasNextPage = pagePlusOne.Length > query.PageSize;
        var pagedItems = hasNextPage
            ? pagePlusOne.Take(query.PageSize).ToArray()
            : pagePlusOne;

        return new ListStravaActivitiesResult
        {
            Page = query.Page,
            PageSize = query.PageSize,
            HasNextPage = hasNextPage,
            Items = pagedItems
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> ListActivityIdsAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endInclusive,
        CancellationToken cancellationToken = default)
    {
        if (startInclusive > endInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(startInclusive), "The start of the range must be less than or equal to the end of the range.");
        }

        var activities = await SearchAsync(startInclusive, endInclusive, cancellationToken);
        return activities
            .Select(static activity => activity.Id)
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Saves all activities into monthly partition files derived from <see cref="StravaActivity.StartDate"/>.
    /// Existing partition files are loaded first, activities are upserted by id, and the result is written
    /// back sorted by <see cref="StravaActivity.StartDate"/>.
    /// </summary>
    /// <param name="activities">The activities to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public async Task SaveAllAsync(IReadOnlyList<StravaActivity> activities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activities);

        if (activities.Count == 0)
        {
            return;
        }

        foreach (var group in activities
            .GroupBy(GetRequiredPartition)
            .OrderBy(static group => group.Key.Year)
            .ThenBy(static group => group.Key.Month))
        {
            var path = BuildPath(group.Key);
            var (existing, eTag) = await LoadActivitiesWithETagAsync(path, cancellationToken);
            var activitiesById = existing.ToDictionary(static activity => activity.Id);

            foreach (var activity in group)
            {
                activitiesById[activity.Id] = activity;
            }

            await objectStore.StoreObjectAsync(path, SortActivities(activitiesById.Values), overwrite: true, ifMatchETag: eTag, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Searches saved activity files that overlap the requested month range and returns activities whose
    /// <see cref="StravaActivity.StartDate"/> falls within the inclusive bounds.
    /// </summary>
    /// <param name="startInclusive">The inclusive lower bound of the search range.</param>
    /// <param name="endInclusive">The inclusive upper bound of the search range.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching activities ordered by start date and id.</returns>
    public async Task<IReadOnlyList<StravaActivity>> SearchAsync(DateTimeOffset startInclusive, DateTimeOffset endInclusive, CancellationToken cancellationToken = default)
    {
        if (startInclusive > endInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(startInclusive), "The start of the range must be less than or equal to the end of the range.");
        }

        var objectPaths = await objectStore.ListObjectsAsync(prefix, cancellationToken);
        var startPartition = new ActivityPartition(startInclusive.Year, startInclusive.Month);
        var endPartition = new ActivityPartition(endInclusive.Year, endInclusive.Month);
        var matches = new List<StravaActivity>();

        foreach (var objectPath in objectPaths)
        {
            if (!TryParsePartition(objectPath, out var partition) || partition.CompareTo(startPartition) < 0 || partition.CompareTo(endPartition) > 0)
            {
                continue;
            }

            var activities = await LoadActivitiesAsync(objectPath, cancellationToken);
            matches.AddRange(activities.Where(activity =>
                activity.StartDate is not null &&
                activity.StartDate.Value >= startInclusive &&
                activity.StartDate.Value <= endInclusive));
        }

        return SortActivities(matches);
    }

    /// <summary>
    /// Deletes activities from the monthly partition files derived from <see cref="StravaActivity.StartDate"/>.
    /// Any partition file left empty after deletion is removed from the object store.
    /// </summary>
    /// <param name="activities">The activities to delete.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public async Task DeleteAllAsync(IReadOnlyList<StravaActivity> activities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activities);

        if (activities.Count == 0)
        {
            return;
        }

        foreach (var group in activities
            .GroupBy(GetRequiredPartition)
            .OrderBy(static group => group.Key.Year)
            .ThenBy(static group => group.Key.Month))
        {
            var path = BuildPath(group.Key);
            var existingObject = await objectStore.TryReadObjectAsync<List<StravaActivity>>(path, cancellationToken: cancellationToken);
            if (!existingObject.IsFound || existingObject.Value is null)
            {
                continue;
            }

            var existing = existingObject.Value;

            var idsToDelete = group.Select(static activity => activity.Id).ToHashSet();
            var remaining = existing
                .Where(activity => !idsToDelete.Contains(activity.Id))
                .ToList();

            if (remaining.Count == 0)
            {
                await objectStore.TryDeleteObjectAsync(path, cancellationToken);
                continue;
            }

            await objectStore.StoreObjectAsync(path, SortActivities(remaining), overwrite: true, ifMatchETag: existingObject.ETag, cancellationToken: cancellationToken);
        }
    }

    private async Task<List<StravaActivity>> LoadActivitiesAsync(string path, CancellationToken cancellationToken)
    {
        var objectValue = await objectStore.TryReadObjectAsync<List<StravaActivity>>(path, cancellationToken: cancellationToken);
        return objectValue.IsFound && objectValue.Value is not null ? objectValue.Value : [];
    }

    /// <summary>
    /// Loads activities from a partition file and returns both the activities and the ETag for optimistic concurrency.
    /// </summary>
    /// <param name="path">The path to the partition file.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A tuple containing the activities list and the current ETag (null if file doesn't exist).</returns>
    private async Task<(List<StravaActivity> Activities, string? ETag)> LoadActivitiesWithETagAsync(string path, CancellationToken cancellationToken)
    {
        var objectValue = await objectStore.TryReadObjectAsync<List<StravaActivity>>(path, cancellationToken: cancellationToken);
        var activities = objectValue.IsFound && objectValue.Value is not null ? objectValue.Value : [];
        return (activities, objectValue.ETag);
    }

    private string BuildPath(ActivityPartition partition)
    {
        var fileName = $"{FileNamePrefix}{partition.Year}-{partition.Month:D2}{FileNameSuffix}";
        return prefix.Length == 0 ? fileName : $"{prefix}/{fileName}";
    }

    private static IReadOnlyList<StravaActivity> SortActivities(IEnumerable<StravaActivity> activities) => activities
        .OrderBy(static activity => activity.StartDate ?? DateTimeOffset.MinValue)
        .ThenBy(static activity => activity.Id)
        .ToArray();

    private static List<StravaActivity> OrderAscending(IEnumerable<StravaActivity> activities) => activities
        .OrderBy(static activity => activity.StartDate is null)
        .ThenBy(static activity => activity.StartDate)
        .ThenBy(static activity => activity.Id)
        .ToList();

    private static List<StravaActivity> OrderDescending(IEnumerable<StravaActivity> activities) => activities
        .OrderBy(static activity => activity.StartDate is null)
        .ThenByDescending(static activity => activity.StartDate)
        .ThenByDescending(static activity => activity.Id)
        .ToList();

    private static bool Matches(StravaActivity activity, ListStravaActivitiesQuery query)
    {
        if (query.StartDateUtc is not null)
        {
            if (activity.StartDate is null || activity.StartDate.Value < query.StartDateUtc.Value)
            {
                return false;
            }
        }

        if (query.EndDateUtc is not null)
        {
            if (activity.StartDate is null || activity.StartDate.Value > query.EndDateUtc.Value)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.ActivityType))
        {
            var activityType = query.ActivityType.Trim();
            var matchesType = string.Equals(activity.Type, activityType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity.SportType, activityType, StringComparison.OrdinalIgnoreCase);

            if (!matchesType)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPartitionInRange(ActivityPartition partition, DateTimeOffset? startDateUtc, DateTimeOffset? endDateUtc)
    {
        if (startDateUtc is not null)
        {
            var startPartition = new ActivityPartition(startDateUtc.Value.Year, startDateUtc.Value.Month);
            if (partition.CompareTo(startPartition) < 0)
            {
                return false;
            }
        }

        if (endDateUtc is not null)
        {
            var endPartition = new ActivityPartition(endDateUtc.Value.Year, endDateUtc.Value.Month);
            if (partition.CompareTo(endPartition) > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateListQuery(ListStravaActivitiesQuery query)
    {
        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Page), "Page must be greater than or equal to 1.");
        }

        if (query.PageSize < 1 || query.PageSize > ListStravaActivitiesQuery.MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(query.PageSize), $"PageSize must be between 1 and {ListStravaActivitiesQuery.MaxPageSize}.");
        }

        if (query.StartDateUtc is not null && query.EndDateUtc is not null && query.StartDateUtc > query.EndDateUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(query.StartDateUtc), "StartDateUtc must be less than or equal to EndDateUtc.");
        }
    }

    private static ActivityPartition GetRequiredPartition(StravaActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (activity.StartDate is null)
        {
            throw new InvalidOperationException("StravaActivity.StartDate must be set when saving or deleting activities.");
        }

        return new ActivityPartition(activity.StartDate.Value.Year, activity.StartDate.Value.Month);
    }

    /// <summary>
    /// Normalizes an optional object store prefix to use forward slashes with no surrounding separators.
    /// </summary>
    /// <param name="prefix">The prefix to normalize.</param>
    /// <returns>The normalized prefix, or an empty string.</returns>
    private static string NormalizePrefix(string prefix) => string.IsNullOrWhiteSpace(prefix)
        ? string.Empty
        : prefix.Replace('\\', '/').Trim('/');

    /// <summary>
    /// Parses a monthly partition from an object store path for this repository.
    /// </summary>
    /// <param name="objectPath">The object store path to inspect.</param>
    /// <param name="partition">The parsed partition when successful.</param>
    /// <returns><see langword="true"/> when the path matches this repository's partition format; otherwise, <see langword="false"/>.</returns>
    private bool TryParsePartition(string objectPath, out ActivityPartition partition)
    {
        var expectedPrefix = prefix.Length == 0 ? string.Empty : $"{prefix}/";
        if (!objectPath.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            partition = default;
            return false;
        }

        var fileName = objectPath[expectedPrefix.Length..];
        if (!fileName.StartsWith(FileNamePrefix, StringComparison.Ordinal) ||
            !fileName.EndsWith(FileNameSuffix, StringComparison.Ordinal) ||
            fileName.Contains('/'))
        {
            partition = default;
            return false;
        }

        var partitionText = fileName[FileNamePrefix.Length..^FileNameSuffix.Length];
        var segments = partitionText.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 ||
            !int.TryParse(segments[0], out var year) ||
            !int.TryParse(segments[1], out var month) ||
            month is < 1 or > 12)
        {
            partition = default;
            return false;
        }

        partition = new ActivityPartition(year, month);
        return true;
    }

    private readonly record struct ActivityPartition(int Year, int Month) : IComparable<ActivityPartition>
    {
        public int CompareTo(ActivityPartition other)
        {
            var yearComparison = Year.CompareTo(other.Year);
            return yearComparison != 0 ? yearComparison : Month.CompareTo(other.Month);
        }
    }
}