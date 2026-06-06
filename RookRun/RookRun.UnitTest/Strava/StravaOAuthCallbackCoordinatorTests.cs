using RookRun.Strava.Auth.Coordination;
using RookRun.Strava.Auth.Exceptions;
namespace RookRun.UnitTest.Strava;

public class StravaOAuthCallbackCoordinatorTests
{
    [Fact]
    public async Task CompleteSuccess_CompletesPendingFlow()
    {
        var coordinator = new StravaOAuthCallbackCoordinator();
        var pendingFlow = coordinator.CreatePendingFlow("state-1");
        const string expected = "authorization-code";

        var completed = coordinator.CompleteSuccess("state-1", expected);
        var actual = await pendingFlow.Completion.Task;

        Assert.True(completed);
        Assert.Equal(expected, actual);
        Assert.False(coordinator.TryGetPendingFlow("state-1", out _));
    }

    [Fact]
    public async Task CompleteFailure_FaultsPendingFlow()
    {
        var coordinator = new StravaOAuthCallbackCoordinator();
        var pendingFlow = coordinator.CreatePendingFlow("state-2");

        var completed = coordinator.CompleteFailure("state-2", new InvalidOperationException("boom"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pendingFlow.Completion.Task);
        Assert.True(completed);
        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task Timeout_FaultsPendingFlowWithTimeoutException()
    {
        var coordinator = new StravaOAuthCallbackCoordinator();
        var pendingFlow = coordinator.CreatePendingFlow("state-3");

        var timedOut = coordinator.Timeout("state-3");

        var exception = await Assert.ThrowsAsync<StravaOAuthTimeoutException>(async () => await pendingFlow.Completion.Task);
        Assert.True(timedOut);
        Assert.Equal("The Strava authorization flow timed out.", exception.Message);
    }

    [Fact]
    public void TryGetPendingFlow_ReturnsFalseForUnknownState()
    {
        var coordinator = new StravaOAuthCallbackCoordinator();

        var found = coordinator.TryGetPendingFlow("missing", out _);

        Assert.False(found);
    }
}
