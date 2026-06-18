using RookRun.ObjectStore;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="ObjectStoreStravaActivityStreamsRepository"/>.
/// </summary>
public sealed class ObjectStoreStravaActivityStreamsRepositoryTests
{
    /// <summary>
    /// Verifies saving and retrieving streams by activity ID.
    /// </summary>
    [Fact]
    public async Task SaveAsync_AndGetByActivityIdAsync_RoundTripsStreams()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityStreamsRepository(store);

        var streams = CreateStreams(123, streamCount: 2);

        await repository.SaveAsync(streams);
        var retrieved = await repository.GetByActivityIdAsync(123);

        Assert.NotNull(retrieved);
        Assert.Equal(123, retrieved!.ActivityId);
        Assert.Equal(2, retrieved.Streams.Count);
    }

    /// <summary>
    /// Verifies retrieving unknown activity returns null.
    /// </summary>
    [Fact]
    public async Task GetByActivityIdAsync_ReturnsNullWhenNotFound()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityStreamsRepository(store);

        var retrieved = await repository.GetByActivityIdAsync(999);

        Assert.Null(retrieved);
    }

    /// <summary>
    /// Verifies ExistsAsync behavior.
    /// </summary>
    [Fact]
    public async Task ExistsAsync_ReturnsTrueWhenStreamsExist()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityStreamsRepository(store);

        await repository.SaveAsync(CreateStreams(10, streamCount: 1));

        Assert.True(await repository.ExistsAsync(10));
        Assert.False(await repository.ExistsAsync(11));
    }

    /// <summary>
    /// Verifies ListActivityIdsAsync returns cached IDs.
    /// </summary>
    [Fact]
    public async Task ListActivityIdsAsync_ReturnsCachedActivityIds()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityStreamsRepository(store);

        await repository.SaveAsync(CreateStreams(100, streamCount: 1));
        await repository.SaveAsync(CreateStreams(200, streamCount: 1));

        var ids = await repository.ListActivityIdsAsync();

        Assert.Equal([100L, 200L], ids.OrderBy(id => id).ToArray());
    }

    /// <summary>
    /// Verifies DeleteAsync removes cached streams.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_RemovesStreams()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityStreamsRepository(store);

        await repository.SaveAsync(CreateStreams(300, streamCount: 1));
        Assert.True(await repository.ExistsAsync(300));

        await repository.DeleteAsync(300);

        Assert.False(await repository.ExistsAsync(300));
    }

    /// <summary>
    /// Verifies SaveAsync overwrites existing stream document.
    /// </summary>
    [Fact]
    public async Task SaveAsync_OverwritesExistingStreams()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityStreamsRepository(store);

        await repository.SaveAsync(CreateStreams(400, streamCount: 1));
        await repository.SaveAsync(CreateStreams(400, streamCount: 3));

        var retrieved = await repository.GetByActivityIdAsync(400);

        Assert.NotNull(retrieved);
        Assert.Equal(3, retrieved!.Streams.Count);
    }

    /// <summary>
    /// Verifies custom prefix is included in storage path.
    /// </summary>
    [Fact]
    public async Task Constructor_WithPrefix_StoresToPrefixedPath()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityStreamsRepository(store, "custom/prefix");

        await repository.SaveAsync(CreateStreams(500, streamCount: 1));

        var objects = await store.ListObjectsAsync("custom/prefix/");
        Assert.Single(objects);
        Assert.Contains("500", objects[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies custom prefix with trailing slash does not produce duplicate slashes in paths.
    /// </summary>
    [Fact]
    public async Task Constructor_WithPrefixEndingInSlash_StoresToNormalizedPath()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityStreamsRepository(store, "custom/prefix/");

        await repository.SaveAsync(CreateStreams(501, streamCount: 1));

        var objects = await store.ListObjectsAsync("custom/prefix/");
        Assert.Single(objects);
        Assert.DoesNotContain("//", objects[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates stream test data with deterministic content.
    /// </summary>
    private static StravaActivityStreams CreateStreams(long activityId, int streamCount)
    {
        var streams = new Dictionary<string, StravaStreamData>(StringComparer.Ordinal);

        if (streamCount >= 1)
        {
            streams[StravaStreamKeys.Time] = new StravaStreamData
            {
                Type = StravaStreamKeys.Time,
                SeriesType = "time",
                Resolution = "high",
                OriginalSize = 2,
                Data = System.Text.Json.JsonSerializer.SerializeToElement(new[] { 0, 1 })
            };
        }

        if (streamCount >= 2)
        {
            streams[StravaStreamKeys.Distance] = new StravaStreamData
            {
                Type = StravaStreamKeys.Distance,
                SeriesType = "distance",
                Resolution = "high",
                OriginalSize = 2,
                Data = System.Text.Json.JsonSerializer.SerializeToElement(new[] { 1.0, 2.0 })
            };
        }

        if (streamCount >= 3)
        {
            streams[StravaStreamKeys.Heartrate] = new StravaStreamData
            {
                Type = StravaStreamKeys.Heartrate,
                SeriesType = "time",
                Resolution = "high",
                OriginalSize = 2,
                Data = System.Text.Json.JsonSerializer.SerializeToElement(new[] { 90, 91 })
            };
        }

        return new StravaActivityStreams
        {
            ActivityId = activityId,
            FetchedUtc = DateTimeOffset.UtcNow,
            RequestedKeys = StravaStreamKeys.DefaultPhase1,
            Streams = streams
        };
    }
}
