using Microsoft.Extensions.Logging.Abstractions;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Sync;
using System.Net;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="StravaRetryPolicy"/>.
/// </summary>
public sealed class StravaRetryPolicyTests
{
    /// <summary>
    /// Verifies the generic overload returns immediately on first success and does not invoke delay.
    /// </summary>
    [Fact]
    public async Task ExecuteWithRetryAsync_Generic_ReturnsImmediatelyOnSuccess()
    {
        var delayCalls = 0;

        var result = await StravaRetryPolicy.ExecuteWithRetryAsync(
            _ => Task.FromResult(42),
            NullLogger.Instance,
            "immediate success",
            CancellationToken.None,
            (delay, token) =>
            {
                delayCalls++;
                return Task.CompletedTask;
            });

        Assert.Equal(42, result);
        Assert.Equal(0, delayCalls);
    }

    /// <summary>
    /// Verifies the non-generic overload retries after a rate-limit exception and eventually succeeds.
    /// </summary>
    [Fact]
    public async Task ExecuteWithRetryAsync_NonGeneric_RetriesAndSucceeds()
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();

        await StravaRetryPolicy.ExecuteWithRetryAsync(
            _ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw CreateRateLimitException();
                }

                return Task.CompletedTask;
            },
            NullLogger.Instance,
            "non-generic retry",
            CancellationToken.None,
            (delay, token) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            },
            maxAttempts: 3);

        Assert.Equal(2, attempts);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(5), delays[0]);
    }

    /// <summary>
    /// Verifies the generic overload throws after max attempts are exhausted.
    /// </summary>
    [Fact]
    public async Task ExecuteWithRetryAsync_Generic_ThrowsAfterMaxAttempts()
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();

        await Assert.ThrowsAsync<RateLimitException>(() =>
            StravaRetryPolicy.ExecuteWithRetryAsync(
                _ =>
                {
                    attempts++;
                    throw CreateRateLimitException();
                },
                NullLogger.Instance,
                "exhaust retries",
                CancellationToken.None,
                (delay, token) =>
                {
                    delays.Add(delay);
                    return Task.CompletedTask;
                },
                maxAttempts: 2));

        Assert.Equal(2, attempts);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(5), delays[0]);
    }

    /// <summary>
    /// Verifies cancellation during delay is propagated to the caller.
    /// </summary>
    [Fact]
    public async Task ExecuteWithRetryAsync_Generic_PropagatesCancellationDuringDelay()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            StravaRetryPolicy.ExecuteWithRetryAsync(
                _ => throw CreateRateLimitException(),
                NullLogger.Instance,
                "cancel during delay",
                cts.Token,
                (delay, token) => Task.FromCanceled(token),
                maxAttempts: 3));
    }

    /// <summary>
    /// Creates a reusable Strava rate-limit exception for retry tests.
    /// </summary>
    private static RateLimitException CreateRateLimitException()
    {
        return new RateLimitException(
            HttpStatusCode.TooManyRequests,
            "rate limited",
            new Dictionary<string, string[]>());
    }
}
