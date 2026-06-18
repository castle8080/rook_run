using Microsoft.Extensions.Logging;
using Moq;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;
using System.Net;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="SyncStravaActivityStreamsSynchronizer"/>.
/// </summary>
public sealed class SyncStravaActivityStreamsSynchronizerTests
{
    /// <summary>
    /// Verifies synchronizer fetches and saves new stream documents.
    /// </summary>
    [Fact]
    public async Task SyncAsync_FetchesAndSavesNewStreams()
    {
        var logger = new Mock<ILogger<SyncStravaActivityStreamsSynchronizer>>();
        var client = new Mock<IStravaActivityStreamsClient>();
        var repository = new Mock<IStravaActivityStreamsRepository>();

        var streams1 = CreateStreams(123, 1);
        var streams2 = CreateStreams(456, 2);

        client.Setup(c => c.GetActivityStreamsAsync(123, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(streams1);
        client.Setup(c => c.GetActivityStreamsAsync(456, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(streams2);

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());
        repository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityStreams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityStreamsSynchronizer(logger.Object, client.Object, repository.Object);

        var result = await synchronizer.SyncAsync([123, 456]);

        Assert.Equal(2, result);
        repository.Verify(r => r.SaveAsync(streams1, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(streams2, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies synchronizer skips stream documents that are already cached.
    /// </summary>
    [Fact]
    public async Task SyncAsync_SkipsCachedStreams()
    {
        var logger = new Mock<ILogger<SyncStravaActivityStreamsSynchronizer>>();
        var client = new Mock<IStravaActivityStreamsClient>();
        var repository = new Mock<IStravaActivityStreamsRepository>();

        client.Setup(c => c.GetActivityStreamsAsync(111, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStreams(111, 1));

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([999]);
        repository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityStreams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityStreamsSynchronizer(logger.Object, client.Object, repository.Object);

        var result = await synchronizer.SyncAsync([999, 111]);

        Assert.Equal(1, result);
        client.Verify(c => c.GetActivityStreamsAsync(999, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.GetActivityStreamsAsync(111, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies empty stream responses are treated as complete and cached.
    /// </summary>
    [Fact]
    public async Task SyncAsync_CachesEmptyStreamPayloads()
    {
        var logger = new Mock<ILogger<SyncStravaActivityStreamsSynchronizer>>();
        var client = new Mock<IStravaActivityStreamsClient>();
        var repository = new Mock<IStravaActivityStreamsRepository>();

        var emptyStreams = new StravaActivityStreams
        {
            ActivityId = 222,
            FetchedUtc = DateTimeOffset.UtcNow,
            RequestedKeys = StravaStreamKeys.DefaultPhase1,
            Streams = new Dictionary<string, StravaStreamData>(StringComparer.Ordinal)
        };

        client.Setup(c => c.GetActivityStreamsAsync(222, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyStreams);

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());
        repository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityStreams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityStreamsSynchronizer(logger.Object, client.Object, repository.Object);

        var result = await synchronizer.SyncAsync([222]);

        Assert.Equal(1, result);
        repository.Verify(r => r.SaveAsync(It.Is<StravaActivityStreams>(s => s.ActivityId == 222 && s.Streams.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies null responses (for example, 404) are skipped.
    /// </summary>
    [Fact]
    public async Task SyncAsync_SkipsNullResponses()
    {
        var logger = new Mock<ILogger<SyncStravaActivityStreamsSynchronizer>>();
        var client = new Mock<IStravaActivityStreamsClient>();
        var repository = new Mock<IStravaActivityStreamsRepository>();

        client.Setup(c => c.GetActivityStreamsAsync(333, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StravaActivityStreams?)null);

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());

        var synchronizer = new SyncStravaActivityStreamsSynchronizer(logger.Object, client.Object, repository.Object);

        var result = await synchronizer.SyncAsync([333]);

        Assert.Equal(0, result);
        repository.Verify(r => r.SaveAsync(It.IsAny<StravaActivityStreams>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies synchronizer continues on partial failures.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ContinuesOnPartialFailures()
    {
        var logger = new Mock<ILogger<SyncStravaActivityStreamsSynchronizer>>();
        var client = new Mock<IStravaActivityStreamsClient>();
        var repository = new Mock<IStravaActivityStreamsRepository>();

        client.Setup(c => c.GetActivityStreamsAsync(1, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStreams(1, 1));
        client.Setup(c => c.GetActivityStreamsAsync(2, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API error"));
        client.Setup(c => c.GetActivityStreamsAsync(3, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStreams(3, 1));

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());
        repository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityStreams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityStreamsSynchronizer(logger.Object, client.Object, repository.Object);

        var result = await synchronizer.SyncAsync([1, 2, 3]);

        Assert.Equal(2, result);
        repository.Verify(r => r.SaveAsync(It.IsAny<StravaActivityStreams>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// Verifies synchronizer retries rate-limit responses before succeeding.
    /// </summary>
    [Fact]
    public async Task SyncAsync_RetriesRateLimitFailuresBeforeSavingStreams()
    {
        var logger = new Mock<ILogger<SyncStravaActivityStreamsSynchronizer>>();
        var client = new Mock<IStravaActivityStreamsClient>();
        var repository = new Mock<IStravaActivityStreamsRepository>();
        var delayCalls = new List<TimeSpan>();

        var streams = CreateStreams(444, 2);

        client.SetupSequence(c => c.GetActivityStreamsAsync(444, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RateLimitException(HttpStatusCode.TooManyRequests, "rate limited", new Dictionary<string, string[]>()))
            .ReturnsAsync(streams);

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());
        repository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityStreams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityStreamsSynchronizer(
            logger.Object,
            client.Object,
            repository.Object,
            (delay, _) =>
            {
                delayCalls.Add(delay);
                return Task.CompletedTask;
            });

        var result = await synchronizer.SyncAsync([444]);

        Assert.Equal(1, result);
        Assert.Single(delayCalls);
        Assert.Equal(TimeSpan.FromSeconds(5), delayCalls[0]);
        repository.Verify(r => r.SaveAsync(It.Is<StravaActivityStreams>(s => s.ActivityId == 444), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies synchronizer throws for null activity ID list.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ThrowsForNullActivityIds()
    {
        var logger = new Mock<ILogger<SyncStravaActivityStreamsSynchronizer>>();
        var client = new Mock<IStravaActivityStreamsClient>();
        var repository = new Mock<IStravaActivityStreamsRepository>();

        var synchronizer = new SyncStravaActivityStreamsSynchronizer(logger.Object, client.Object, repository.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() => synchronizer.SyncAsync(null!));
    }

    /// <summary>
    /// Creates deterministic stream test data.
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
