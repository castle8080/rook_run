using RookRun.ObjectStore;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.UnitTest.Strava;

public class ObjectStoreStravaActivitiesRepositoryTests
{
    [Fact]
    public async Task ListAsync_DefaultsToDescendingByStartDate()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivitiesRepository(store, "history");

        await repository.SaveAllAsync([
            CreateActivity(1, new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero), type: "Run"),
            CreateActivity(2, new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero), type: "Ride"),
            CreateActivity(3, new DateTimeOffset(2026, 3, 3, 8, 0, 0, TimeSpan.Zero), type: "Run")
        ]);

        var result = await repository.ListAsync(new ListStravaActivitiesQuery());

        Assert.False(result.HasNextPage);
        Assert.Collection(
            result.Items,
            activity => Assert.Equal(3, activity.Id),
            activity => Assert.Equal(2, activity.Id),
            activity => Assert.Equal(1, activity.Id));
    }

    [Fact]
    public async Task ListAsync_AppliesPageAndPageSize()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivitiesRepository(store, "history");

        await repository.SaveAllAsync([
            CreateActivity(1, new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero), type: "Run"),
            CreateActivity(2, new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero), type: "Run"),
            CreateActivity(3, new DateTimeOffset(2026, 3, 3, 8, 0, 0, TimeSpan.Zero), type: "Run"),
            CreateActivity(4, new DateTimeOffset(2026, 3, 4, 8, 0, 0, TimeSpan.Zero), type: "Run")
        ]);

        var result = await repository.ListAsync(new ListStravaActivitiesQuery
        {
            Page = 2,
            PageSize = 2,
            SortDirection = StravaActivitiesSortDirection.Desc
        });

        Assert.False(result.HasNextPage);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Collection(
            result.Items,
            activity => Assert.Equal(2, activity.Id),
            activity => Assert.Equal(1, activity.Id));

        var firstPage = await repository.ListAsync(new ListStravaActivitiesQuery
        {
            Page = 1,
            PageSize = 2,
            SortDirection = StravaActivitiesSortDirection.Desc
        });

        Assert.True(firstPage.HasNextPage);
    }

    [Fact]
    public async Task ListAsync_FiltersByDateRangeAndActivityType()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivitiesRepository(store, "history");

        await repository.SaveAllAsync([
            CreateActivity(1, new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero), type: "Run"),
            CreateActivity(2, new DateTimeOffset(2026, 3, 15, 8, 0, 0, TimeSpan.Zero), type: "Ride"),
            CreateActivity(3, new DateTimeOffset(2026, 3, 20, 8, 0, 0, TimeSpan.Zero), type: "Run"),
            CreateActivity(4, new DateTimeOffset(2026, 4, 1, 8, 0, 0, TimeSpan.Zero), type: "Run")
        ]);

        var result = await repository.ListAsync(new ListStravaActivitiesQuery
        {
            StartDateUtc = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
            EndDateUtc = new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero),
            ActivityType = "run",
            SortDirection = StravaActivitiesSortDirection.Desc
        });

        Assert.False(result.HasNextPage);
        Assert.Collection(result.Items, activity => Assert.Equal(3, activity.Id));
    }

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

        var march = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-03.json.br");
        var april = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-04.json.br");

        Assert.True(march.IsFound);
        Assert.True(april.IsFound);
        Assert.Collection(
            march.Value!,
            activity =>
            {
                Assert.Equal(1, activity.Id);
                Assert.Equal("updated", activity.Name);
            },
            activity => Assert.Equal(2, activity.Id));
        Assert.Collection(april.Value!, activity => Assert.Equal(3, activity.Id));
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

        var march = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-03.json.br");
        var april = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-04.json.br");

        Assert.True(march.IsNotFound);
        Assert.True(april.IsFound);
        Assert.Collection(april.Value!, activity => Assert.Equal(1, activity.Id));
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

        var march = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-03.json.br");
        var april = await store.TryReadObjectAsync<List<StravaActivity>>("history/strava_activities_2026-04.json.br");

        Assert.True(march.IsFound);
        Assert.Collection(march.Value!, activity => Assert.Equal(1, activity.Id));
        Assert.True(april.IsNotFound);
    }

    private static StravaActivity CreateActivity(
        long id,
        DateTimeOffset startDate,
        string? name = null,
        DateTimeOffset? startDateLocal = null,
        string? type = null,
        string? sportType = null) => new()
    {
        Id = id,
        Name = name,
        Type = type,
        SportType = sportType,
        StartDate = startDate,
        StartDateLocal = startDateLocal ?? startDate
    };
}