using RookRun.Job;

namespace RookRun.UnitTest.Job;

/// <summary>
/// Tests for <see cref="InProcessJobExecutionTracker"/>.
/// </summary>
public sealed class InProcessJobExecutionTrackerTests
{
    /// <summary>
    /// Verifies TryStart throws for invalid job names.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void TryStart_ThrowsForInvalidJobName(string? jobName)
    {
        var tracker = new InProcessJobExecutionTracker();

        Assert.ThrowsAny<ArgumentException>(() => tracker.TryStart(jobName!));
    }

    /// <summary>
    /// Verifies Complete throws for invalid job names.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n")]
    public void Complete_ThrowsForInvalidJobName(string? jobName)
    {
        var tracker = new InProcessJobExecutionTracker();

        Assert.ThrowsAny<ArgumentException>(() => tracker.Complete(jobName!));
    }

    /// <summary>
    /// Verifies first start succeeds and duplicate concurrent start is rejected.
    /// </summary>
    [Fact]
    public void TryStart_ReturnsFalseWhenJobAlreadyRunning()
    {
        var tracker = new InProcessJobExecutionTracker();

        var firstStart = tracker.TryStart("sync-streams");
        var secondStart = tracker.TryStart("sync-streams");

        Assert.True(firstStart);
        Assert.False(secondStart);
    }

    /// <summary>
    /// Verifies different job names can run concurrently.
    /// </summary>
    [Fact]
    public void TryStart_AllowsDifferentJobsConcurrently()
    {
        var tracker = new InProcessJobExecutionTracker();

        var first = tracker.TryStart("job-a");
        var second = tracker.TryStart("job-b");

        Assert.True(first);
        Assert.True(second);
    }

    /// <summary>
    /// Verifies completing a running job allows it to start again.
    /// </summary>
    [Fact]
    public void Complete_AfterStart_AllowsRestart()
    {
        var tracker = new InProcessJobExecutionTracker();

        var started = tracker.TryStart("export");
        tracker.Complete("export");
        var restarted = tracker.TryStart("export");

        Assert.True(started);
        Assert.True(restarted);
    }

    /// <summary>
    /// Verifies completing a non-running job is a safe no-op.
    /// </summary>
    [Fact]
    public void Complete_WhenJobNotRunning_DoesNotThrow()
    {
        var tracker = new InProcessJobExecutionTracker();

        var exception = Record.Exception(() => tracker.Complete("not-running"));

        Assert.Null(exception);
    }
}
