using RookRun.ObjectStore;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="ObjectStoreStravaActivityDetailRepository"/>.
/// </summary>
public sealed class ObjectStoreStravaActivityDetailRepositoryTests
{
    /// <summary>
    /// Verifies saving and retrieving a detail by activity ID.
    /// </summary>
    [Fact]
    public async Task SaveAsync_AndGetByIdAsync_RoundTripsDetail()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityDetailRepository(store);

        var detail = new StravaActivityDetail
        {
            Id = 123,
            Name = "Test Run",
            Distance = 5000.5f,
            MovingTime = 1200,
            ElapsedTime = 1300
        };

        await repository.SaveAsync(detail);
        var retrieved = await repository.GetByIdAsync(123);

        Assert.NotNull(retrieved);
        Assert.Equal(detail.Id, retrieved.Id);
        Assert.Equal(detail.Name, retrieved.Name);
        Assert.Equal(detail.Distance, retrieved.Distance);
    }

    /// <summary>
    /// Verifies GetByIdAsync returns null when detail doesn't exist.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityDetailRepository(store);

        var detail = await repository.GetByIdAsync(999);

        Assert.Null(detail);
    }

    /// <summary>
    /// Verifies ExistsAsync returns true when detail exists.
    /// </summary>
    [Fact]
    public async Task ExistsAsync_ReturnsTrueWhenDetailExists()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityDetailRepository(store);

        var detail = new StravaActivityDetail { Id = 456, Name = "Existing" };
        await repository.SaveAsync(detail);

        var exists = await repository.ExistsAsync(456);

        Assert.True(exists);
    }

    /// <summary>
    /// Verifies ExistsAsync returns false when detail doesn't exist.
    /// </summary>
    [Fact]
    public async Task ExistsAsync_ReturnsFalseWhenDetailNotFound()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityDetailRepository(store);

        var exists = await repository.ExistsAsync(999);

        Assert.False(exists);
    }

    /// <summary>
    /// Verifies ListActivityIdsAsync returns activity IDs from cached detail file names.
    /// </summary>
    [Fact]
    public async Task ListActivityIdsAsync_ReturnsCachedActivityIds()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityDetailRepository(store);

        await repository.SaveAsync(new StravaActivityDetail { Id = 101, Name = "First" });
        await repository.SaveAsync(new StravaActivityDetail { Id = 202, Name = "Second" });

        var activityIds = await repository.ListActivityIdsAsync();

        Assert.Equal([101L, 202L], activityIds.OrderBy(id => id).ToArray());
    }

    /// <summary>
    /// Verifies DeleteAsync removes a detail.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_RemovesDetail()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityDetailRepository(store);

        var detail = new StravaActivityDetail { Id = 789, Name = "To Delete" };
        await repository.SaveAsync(detail);

        var existsBefore = await repository.ExistsAsync(789);
        await repository.DeleteAsync(789);
        var existsAfter = await repository.ExistsAsync(789);

        Assert.True(existsBefore);
        Assert.False(existsAfter);
    }

    /// <summary>
    /// Verifies SaveAsync overwrites existing detail.
    /// </summary>
    [Fact]
    public async Task SaveAsync_OverwritesExistingDetail()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityDetailRepository(store);

        var detail1 = new StravaActivityDetail { Id = 111, Name = "First", Distance = 1000 };
        var detail2 = new StravaActivityDetail { Id = 111, Name = "Updated", Distance = 2000 };

        await repository.SaveAsync(detail1);
        var retrieved1 = await repository.GetByIdAsync(111);

        await repository.SaveAsync(detail2);
        var retrieved2 = await repository.GetByIdAsync(111);

        Assert.Equal("First", retrieved1!.Name);
        Assert.Equal("Updated", retrieved2!.Name);
        Assert.Equal(2000, retrieved2.Distance);
    }

    /// <summary>
    /// Verifies custom prefix is included in storage path.
    /// </summary>
    [Fact]
    public async Task Constructor_WithPrefix_StoresToPrefixedPath()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityDetailRepository(store, "custom/prefix");

        var detail = new StravaActivityDetail { Id = 222, Name = "Prefixed" };
        await repository.SaveAsync(detail);

        var objects = await store.ListObjectsAsync("custom/prefix/");
        
        Assert.Single(objects);
        Assert.Contains("222", objects.First(), StringComparison.Ordinal);
    }
}
