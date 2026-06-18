using Microsoft.Extensions.Logging;
using RookRun.Common.Exceptions;

namespace RookRun.Strava.Sync;

/// <summary>
/// Provides reusable retry behavior for Strava synchronizers.
/// </summary>
public static class StravaRetryPolicy
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Executes an operation with exponential backoff when a Strava rate-limit exception is thrown.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="logger">Logger used for retry diagnostics.</param>
    /// <param name="operationName">A human-readable operation name for logs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="delayAsync">Optional delay function for testing.</param>
    /// <param name="maxAttempts">Maximum total attempts, including the initial one.</param>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        int maxAttempts = 12)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        delayAsync ??= Task.Delay;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (RateLimitException ex) when (attempt < maxAttempts)
            {
                var delay = GetDelay(attempt);
                logger.LogWarning(
                    ex,
                    "Strava rate limit hit during {OperationName}. Retry attempt {Attempt}/{MaxAttempts} in {Delay}.",
                    operationName,
                    attempt,
                    maxAttempts,
                    delay);

                await delayAsync(delay, cancellationToken);
            }
            catch (RateLimitException ex)
            {
                logger.LogWarning(
                    ex,
                    "Strava rate limit persisted during {OperationName} after {MaxAttempts} attempts. Giving up.",
                    operationName,
                    maxAttempts);
                throw;
            }
        }
    }

    /// <summary>
    /// Executes an operation with exponential backoff when a Strava rate-limit exception is thrown.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="logger">Logger used for retry diagnostics.</param>
    /// <param name="operationName">A human-readable operation name for logs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="delayAsync">Optional delay function for testing.</param>
    /// <param name="maxAttempts">Maximum total attempts, including the initial one.</param>
    public static Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        int maxAttempts = 12)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return ExecuteWithRetryAsync(
            async token =>
            {
                await operation(token);
                return true;
            },
            logger,
            operationName,
            cancellationToken,
            delayAsync,
            maxAttempts);
    }

    /// <summary>
    /// Calculates the retry delay for a given retry attempt.
    /// </summary>
    private static TimeSpan GetDelay(int attempt)
    {
        var delaySeconds = Math.Min(InitialDelay.TotalSeconds * Math.Pow(2, attempt - 1), MaxDelay.TotalSeconds);
        return TimeSpan.FromSeconds(delaySeconds);
    }
}