using RookRun.Common.Exceptions;
using RookRun.ObjectStore;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="ObjectStoreStravaActivityImageIdIndexRepository"/>.
/// </summary>
public sealed class ObjectStoreStravaActivityImageIdIndexRepositoryTests
{
    /// <summary>
    /// Verifies LoadAsync returns an empty index when no object exists.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ReturnsEmptyIndexWhenMissing()
    {
        var repository = new ObjectStoreStravaActivityImageIdIndexRepository(new InMemoryObjectStore());

        var (index, eTag) = await repository.LoadAsync();

        Assert.NotNull(index);
        Assert.Empty(index.Items);
        Assert.Null(eTag);
    }

    /// <summary>
    /// Verifies SaveAsync and LoadAsync round-trip index contents.
    /// </summary>
    [Fact]
    public async Task SaveAsync_AndLoadAsync_RoundTripsIndex()
    {
        var repository = new ObjectStoreStravaActivityImageIdIndexRepository(new InMemoryObjectStore());
        var index = new StravaActivityImageIdIndex
        {
            UpdatedUtc = new DateTimeOffset(2026, 6, 18, 0, 0, 0, TimeSpan.Zero),
            Items = new Dictionary<string, List<string>>
            {
                ["123"] = ["img-b", "img-a", "img-a"],
                ["124"] = []
            }
        };

        await repository.SaveAsync(index, ifMatchETag: null);
        var (loaded, eTag) = await repository.LoadAsync();

        Assert.NotNull(eTag);
        Assert.Equal(index.UpdatedUtc, loaded.UpdatedUtc);
        Assert.Equal(["img-a", "img-b"], loaded.Items["123"]);
        Assert.Empty(loaded.Items["124"]);
    }

    /// <summary>
    /// Verifies SaveAsync rejects stale ETags.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithStaleETag_ThrowsPreconditionFailedException()
    {
        var repository = new ObjectStoreStravaActivityImageIdIndexRepository(new InMemoryObjectStore());

        await repository.SaveAsync(new StravaActivityImageIdIndex
        {
            Items = new Dictionary<string, List<string>>
            {
                ["123"] = ["img-1"]
            }
        }, ifMatchETag: null);

        await Assert.ThrowsAsync<PreconditionFailedException>(async () =>
            await repository.SaveAsync(new StravaActivityImageIdIndex
            {
                Items = new Dictionary<string, List<string>>
                {
                    ["123"] = ["img-2"]
                }
            }, ifMatchETag: "v:999"));
    }

    /// <summary>
    /// Verifies SaveAsync succeeds with the current ETag.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithCurrentETag_Succeeds()
    {
        var repository = new ObjectStoreStravaActivityImageIdIndexRepository(new InMemoryObjectStore());

        await repository.SaveAsync(new StravaActivityImageIdIndex
        {
            Items = new Dictionary<string, List<string>>
            {
                ["123"] = ["img-1"]
            }
        }, ifMatchETag: null);

        var (_, eTag) = await repository.LoadAsync();

        await repository.SaveAsync(new StravaActivityImageIdIndex
        {
            Items = new Dictionary<string, List<string>>
            {
                ["123"] = ["img-1", "img-2"]
            }
        }, ifMatchETag: eTag);

        var (loaded, _) = await repository.LoadAsync();
        Assert.Equal(["img-1", "img-2"], loaded.Items["123"]);
    }

    /// <summary>
    /// Verifies SaveAsync with a prefix writes under the prefixed path.
    /// </summary>
    [Fact]
    public async Task Constructor_WithPrefix_StoresToPrefixedPath()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageIdIndexRepository(store, "custom/prefix");

        await repository.SaveAsync(new StravaActivityImageIdIndex
        {
            Items = new Dictionary<string, List<string>>
            {
                ["123"] = ["img-1"]
            }
        }, ifMatchETag: null);

        var objects = await store.ListObjectsAsync("custom/prefix/");

        Assert.Single(objects);
        Assert.Contains("activity_id_image_ids.json.br", objects.First(), StringComparison.Ordinal);
    }
}