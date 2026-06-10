using RookRun.ObjectStore;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.UnitTest.Strava;

public class ObjectStoreStravaActivitiesRepositoryTests
{
    [Fact]
    public async Task SaveAllAsync_UpsertsByIdWithinEachMonthAndSortsByStartDate()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivitiesRepository(store, "history");

        await repository.SaveAllAsync([
            CreateActivity(2, new DateTimeOffset(2026, 3, 20, 8, 0, 0, TimeSpan.FromHours(-7)), "later"),
            CreateActivity(1, new DateTimeOffset(2026, 3, 10, 6, 0, 0, TimeSpan.FromHours(-7)), "original")
        ]);

        await repository.SaveAllAsync([
            CreateActivity(3, new DateTimeOffset(2026, 4, 1, 7, 30, 0, TimeSpan.FromHours(-7)), "april"),
            CreateActivity(1, new DateTimeOffset(2026, 3, 5, 6, 0, 0, TimeSpan.FromHours(-7)), "updated")
        ]);

        var march = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-3.json.br");
        var april = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-4.json.br");

        Assert.NotNull(march);
        Assert.NotNull(april);
        Assert.Collection(
            march!,
            activity =>
            {
                Assert.Equal(1, activity.Id);
                Assert.Equal("updated", activity.Name);
            },
            activity => Assert.Equal(2, activity.Id));
        Assert.Collection(april!, activity => Assert.Equal(3, activity.Id));
    }

    [Fact]
    public async Task SaveAllAsync_UsesStartDateForPartition()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivitiesRepository(store, "history");

        await repository.SaveAllAsync([
            CreateActivity(
                1,
                new DateTimeOffset(2026, 4, 1, 6, 30, 0, TimeSpan.Zero),
                startDateLocal: new DateTimeOffset(2026, 3, 31, 23, 30, 0, TimeSpan.FromHours(-7)))
        ]);

        var march = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-3.json.br");
        var april = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-4.json.br");

        Assert.Null(march);
        Assert.NotNull(april);
        Assert.Collection(april!, activity => Assert.Equal(1, activity.Id));
    }

    [Fact]
    public async Task SearchAsync_LoadsParticipatingFilesAndFiltersByInclusiveRange()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivitiesRepository(store, "history");

        await repository.SaveAllAsync([
            CreateActivity(1, new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.FromHours(-7))),
            CreateActivity(2, new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.FromHours(-7))),
            CreateActivity(3, new DateTimeOffset(2026, 4, 2, 13, 0, 0, TimeSpan.FromHours(-7))),
            CreateActivity(4, new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.FromHours(-7)))
        ]);

        var results = await repository.SearchAsync(
            new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.FromHours(-7)),
            new DateTimeOffset(2026, 4, 2, 13, 0, 0, TimeSpan.FromHours(-7)));

        Assert.Collection(
            results,
            activity => Assert.Equal(2, activity.Id),
            activity => Assert.Equal(3, activity.Id));
    }

    [Fact]
    public async Task DeleteAllAsync_RemovesIdsByPartitionAndIgnoresMissingActivities()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivitiesRepository(store, "history");

        await repository.SaveAllAsync([
            CreateActivity(1, new DateTimeOffset(2026, 3, 5, 8, 0, 0, TimeSpan.FromHours(-7))),
            CreateActivity(2, new DateTimeOffset(2026, 3, 20, 8, 0, 0, TimeSpan.FromHours(-7))),
            CreateActivity(3, new DateTimeOffset(2026, 4, 1, 8, 0, 0, TimeSpan.FromHours(-7)))
        ]);

        await repository.DeleteAllAsync([
            CreateActivity(2, new DateTimeOffset(2026, 3, 20, 8, 0, 0, TimeSpan.FromHours(-7))),
            CreateActivity(3, new DateTimeOffset(2026, 4, 1, 8, 0, 0, TimeSpan.FromHours(-7))),
            CreateActivity(99, new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.FromHours(-7)))
        ]);

        var march = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-3.json.br");
        var april = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-4.json.br");

        Assert.NotNull(march);
        Assert.Collection(march!, activity => Assert.Equal(1, activity.Id));
        Assert.Null(april);
    }

    private static StravaActivity CreateActivity(long id, DateTimeOffset startDate, string? name = null, DateTimeOffset? startDateLocal = null) => new()
    {
        Id = id,
        Name = name,
        StartDate = startDate,
        StartDateLocal = startDateLocal ?? startDate
    };
}