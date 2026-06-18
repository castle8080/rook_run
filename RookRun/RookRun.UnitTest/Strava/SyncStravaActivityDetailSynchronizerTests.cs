using Microsoft.Extensions.Logging;
using Moq;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="SyncStravaActivityDetailSynchronizer"/>.
/// </summary>
public sealed class SyncStravaActivityDetailSynchronizerTests
{
    /// <summary>
    /// Verifies synchronizer fetches and saves details for provided activity IDs.
    /// </summary>
    [Fact]
    public async Task SyncAsync_FetchesAndSavesNewDetails()
    {
        var logger = new Mock<ILogger<SyncStravaActivityDetailSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var repository = new Mock<IStravaActivityDetailRepository>();

        var detail1 = new StravaActivityDetail { Id = 123, Name = "Activity 1" };
        var detail2 = new StravaActivityDetail { Id = 456, Name = "Activity 2" };

        client.Setup(c => c.GetActivityDetailAsync(123, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail1);
        client.Setup(c => c.GetActivityDetailAsync(456, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail2);

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());
        repository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityDetail>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityDetailSynchronizer(logger.Object, client.Object, repository.Object);

        var result = await synchronizer.SyncAsync([123, 456]);

        Assert.Equal(2, result);
        repository.Verify(r => r.SaveAsync(detail1, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(detail2, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies synchronizer skips details that are already cached.
    /// </summary>
    [Fact]
    public async Task SyncAsync_SkipsCachedDetails()
    {
        var logger = new Mock<ILogger<SyncStravaActivityDetailSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var repository = new Mock<IStravaActivityDetailRepository>();

        var detail = new StravaActivityDetail { Id = 789, Name = "Already Cached" };

        client.Setup(c => c.GetActivityDetailAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([789]);

        repository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityDetail>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityDetailSynchronizer(logger.Object, client.Object, repository.Object);

        var result = await synchronizer.SyncAsync([789, 111]);

        // Only activity 111 should be synced
        client.Verify(c => c.GetActivityDetailAsync(789, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.GetActivityDetailAsync(111, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, result);
    }

    /// <summary>
    /// Verifies synchronizer handles 404 responses (activity not found) gracefully.
    /// </summary>
    [Fact]
    public async Task SyncAsync_HandlesNotFoundResponse()
    {
        var logger = new Mock<ILogger<SyncStravaActivityDetailSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var repository = new Mock<IStravaActivityDetailRepository>();

        client.Setup(c => c.GetActivityDetailAsync(999, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StravaActivityDetail?)null);

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());

        var synchronizer = new SyncStravaActivityDetailSynchronizer(logger.Object, client.Object, repository.Object);

        var result = await synchronizer.SyncAsync([999]);

        // Should not save when API returns null
        repository.Verify(r => r.SaveAsync(It.IsAny<StravaActivityDetail>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(0, result);
    }

    /// <summary>
    /// Verifies synchronizer returns empty list for empty input.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ReturnsZeroForEmptyInput()
    {
        var logger = new Mock<ILogger<SyncStravaActivityDetailSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var repository = new Mock<IStravaActivityDetailRepository>();

        var synchronizer = new SyncStravaActivityDetailSynchronizer(logger.Object, client.Object, repository.Object);

        var result = await synchronizer.SyncAsync([]);

        Assert.Equal(0, result);
        client.Verify(c => c.GetActivityDetailAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies synchronizer continues on individual errors.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ContinuesOnPartialFailures()
    {
        var logger = new Mock<ILogger<SyncStravaActivityDetailSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var repository = new Mock<IStravaActivityDetailRepository>();

        var detail1 = new StravaActivityDetail { Id = 1, Name = "Success" };

        client.Setup(c => c.GetActivityDetailAsync(1, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail1);
        client.Setup(c => c.GetActivityDetailAsync(2, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API error"));

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());
        repository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityDetail>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityDetailSynchronizer(logger.Object, client.Object, repository.Object);

        var result = await synchronizer.SyncAsync([1, 2, 3]);

        // Only the first one should be saved
        repository.Verify(r => r.SaveAsync(It.IsAny<StravaActivityDetail>(), It.IsAny<CancellationToken>()), 
            Times.Once, "Should save first activity");
        Assert.Equal(1, result);
    }

    /// <summary>
    /// Verifies synchronizer retries rate-limit responses before succeeding.
    /// </summary>
    [Fact]
    public async Task SyncAsync_RetriesRateLimitFailuresBeforeSavingDetail()
    {
        var logger = new Mock<ILogger<SyncStravaActivityDetailSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var repository = new Mock<IStravaActivityDetailRepository>();
        var delayCalls = new List<TimeSpan>();

        var detail = new StravaActivityDetail { Id = 321, Name = "Retried" };

        client.SetupSequence(c => c.GetActivityDetailAsync(321, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RateLimitException(System.Net.HttpStatusCode.TooManyRequests, "rate limited", new Dictionary<string, string[]>() ))
            .ReturnsAsync(detail);

        repository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());
        repository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityDetail>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityDetailSynchronizer(
            logger.Object,
            client.Object,
            repository.Object,
            (delay, _) =>
            {
                delayCalls.Add(delay);
                return Task.CompletedTask;
            });

        var result = await synchronizer.SyncAsync([321]);

        Assert.Equal(1, result);
        Assert.Single(delayCalls);
        Assert.Equal(TimeSpan.FromSeconds(5), delayCalls[0]);
        repository.Verify(r => r.SaveAsync(detail, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies synchronizer throws for null activity ID list.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ThrowsForNullActivityIds()
    {
        var logger = new Mock<ILogger<SyncStravaActivityDetailSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var repository = new Mock<IStravaActivityDetailRepository>();

        var synchronizer = new SyncStravaActivityDetailSynchronizer(logger.Object, client.Object, repository.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() => synchronizer.SyncAsync(null!));
    }
}
