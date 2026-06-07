using RookRun.Strava.Client.Auth.Exceptions;
using System.Collections.Concurrent;

namespace RookRun.Strava.Client.Auth.Coordination;

/// <summary>
/// Tracks active OAuth flows and resolves them when callback, timeout, or cancellation events occur.
/// </summary>
public sealed class StravaOAuthCallbackCoordinator
{
    private readonly ConcurrentDictionary<string, PendingAuthorizationFlow> _pendingFlows = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates and registers a new pending authorization flow for the supplied state.
    /// </summary>
    /// <param name="state">The state value associated with the new flow.</param>
    /// <returns>The registered pending flow.</returns>
    public PendingAuthorizationFlow CreatePendingFlow(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var flow = new PendingAuthorizationFlow
        {
            State = state,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)
        };

        if (!_pendingFlows.TryAdd(state, flow))
        {
            throw new InvalidOperationException("A pending Strava OAuth flow already exists for the supplied state.");
        }

        return flow;
    }

    /// <summary>
    /// Looks up a pending flow by its expected state value.
    /// </summary>
    /// <param name="state">The state value to resolve.</param>
    /// <param name="flow">When this method returns, contains the pending flow if found.</param>
    /// <returns><see langword="true"/> when a matching flow exists; otherwise, <see langword="false"/>.</returns>
    internal bool TryGetPendingFlow(string state, out PendingAuthorizationFlow? flow)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            flow = null;
            return false;
        }

        return _pendingFlows.TryGetValue(state, out flow);
    }

    /// <summary>
    /// Completes a pending flow successfully.
    /// </summary>
    /// <param name="state">The state value that identifies the flow.</param>
    /// <param name="authorizationCode">The successful authorization code.</param>
    /// <returns><see langword="true"/> when the flow was completed; otherwise, <see langword="false"/>.</returns>
    public bool CompleteSuccess(string state, string authorizationCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationCode);
        return TryTransition(state, static (flow, payload) => flow.Completion.TrySetResult(payload), authorizationCode);
    }

    /// <summary>
    /// Completes a pending flow with a failure.
    /// </summary>
    /// <param name="state">The state value that identifies the flow.</param>
    /// <param name="exception">The exception to surface to the awaiting caller.</param>
    /// <returns><see langword="true"/> when the flow was completed; otherwise, <see langword="false"/>.</returns>
    public bool CompleteFailure(string state, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return TryTransition(state, static (flow, payload) => flow.Completion.TrySetException(payload), exception);
    }

    /// <summary>
    /// Cancels a pending flow.
    /// </summary>
    /// <param name="state">The state value that identifies the flow.</param>
    /// <param name="cancellationToken">The cancellation token to apply to the waiting task.</param>
    /// <returns><see langword="true"/> when the flow was canceled; otherwise, <see langword="false"/>.</returns>
    public bool Cancel(string state, CancellationToken cancellationToken = default)
    {
        return TryTransition(state, static (flow, payload) => flow.Completion.TrySetCanceled(payload), cancellationToken);
    }

    /// <summary>
    /// Fails a pending flow with a timeout exception.
    /// </summary>
    /// <param name="state">The state value that identifies the flow.</param>
    /// <returns><see langword="true"/> when the flow was transitioned to timeout; otherwise, <see langword="false"/>.</returns>
    public bool Timeout(string state)
    {
        return CompleteFailure(state, new StravaOAuthTimeoutException("The Strava authorization flow timed out."));
    }

    /// <summary>
    /// Removes a pending flow without changing its completion state.
    /// </summary>
    /// <param name="state">The state value that identifies the flow.</param>
    /// <returns><see langword="true"/> when a flow was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        return _pendingFlows.TryRemove(state, out _);
    }

    /// <summary>
    /// Removes a pending flow and attempts to apply the supplied terminal transition.
    /// </summary>
    /// <typeparam name="T">The payload type passed to the transition delegate.</typeparam>
    /// <param name="state">The state value that identifies the flow.</param>
    /// <param name="transition">The terminal transition to apply.</param>
    /// <param name="payload">The payload consumed by the transition.</param>
    /// <returns><see langword="true"/> when the flow was transitioned; otherwise, <see langword="false"/>.</returns>
    private bool TryTransition<T>(string state, Func<PendingAuthorizationFlow, T, bool> transition, T payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        if (!_pendingFlows.TryRemove(state, out var flow))
        {
            return false;
        }

        return transition(flow, payload);
    }
}
