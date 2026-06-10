using Microsoft.Extensions.Logging;
using Moq;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.UnitTest.Strava;

public class StravaActivitiesSynchronizerTests
{
    [Fact]
    public async Task SyncAsync_SavesOnlyNewActivitiesAcrossPages()
    {
        var logger = new Mock<ILogger<StravaActivitiesSynchronizer>>();
        var repository = new Mock<IStravaActivitiesRepository>();
        var client = new Mock<IStravaActivitiesClient>();

        var firstPage = new List<StravaActivity>
        {
            CreateActivity(1, new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero)),
            CreateActivity(2, new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero))
        };
        var secondPage = new List<StravaActivity>
        {
            CreateActivity(3, new DateTimeOffset(2026, 3, 3, 8, 0, 0, TimeSpan.Zero))
        };

        client
            .SetupSequence(x => x.SearchActivitiesAsync(It.IsAny<StravaActivityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage)
            .ReturnsAsync(secondPage)
            .ReturnsAsync([]);

        repository
            .Setup(x => x.SearchAsync(
                new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateActivity(1, new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero))]);

        repository
            .Setup(x => x.SearchAsync(
                new DateTimeOffset(2026, 3, 3, 8, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 3, 8, 0, 0, TimeSpan.Zero),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivity>());

        var sut = new StravaActivitiesSynchronizer(logger.Object, repository.Object, client.Object);

        var result = await sut.SyncAsync();

        Assert.Equal(2, result);

        repository.Verify(
            x => x.SaveAllAsync(
                It.Is<IReadOnlyList<StravaActivity>>(activities => activities.Count == 1 && activities[0].Id == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.SaveAllAsync(
                It.Is<IReadOnlyList<StravaActivity>>(activities => activities.Count == 1 && activities[0].Id == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAsync_StopsWhenActivitiesDoNotHaveStartDate()
    {
        var logger = new Mock<ILogger<StravaActivitiesSynchronizer>>();
        var repository = new Mock<IStravaActivitiesRepository>();
        var client = new Mock<IStravaActivitiesClient>();

        client
            .Setup(x => x.SearchActivitiesAsync(It.IsAny<StravaActivityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StravaActivity { Id = 1 }]);

        var sut = new StravaActivitiesSynchronizer(logger.Object, repository.Object, client.Object);

        var result = await sut.SyncAsync();

        Assert.Equal(0, result);
        repository.Verify(x => x.SearchAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.SaveAllAsync(It.IsAny<IReadOnlyList<StravaActivity>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static StravaActivity CreateActivity(long id, DateTimeOffset startDate) => new()
    {
        Id = id,
        StartDate = startDate,
        StartDateLocal = startDate
    };
}
