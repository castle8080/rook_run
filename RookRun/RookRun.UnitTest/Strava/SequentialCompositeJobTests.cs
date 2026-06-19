using Microsoft.Extensions.Logging.Abstractions;
using RookRun.Job;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="SequentialCompositeJob"/>.
/// </summary>
public sealed class SequentialCompositeJobTests
{
    /// <summary>
    /// Verifies constructor guard clauses for required dependencies and child-job constraints.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsWhenDependenciesAreInvalid()
    {
        var jobs = new List<IJob> { new DelegateJob(_ => Task.CompletedTask) };

        Assert.Throws<ArgumentNullException>(() => new SequentialCompositeJob(null!, jobs));
        Assert.Throws<ArgumentNullException>(() => new SequentialCompositeJob(NullLogger<SequentialCompositeJob>.Instance, null!));
        Assert.Throws<ArgumentException>(() => new SequentialCompositeJob(NullLogger<SequentialCompositeJob>.Instance, []));
        Assert.Throws<ArgumentException>(() => new SequentialCompositeJob(NullLogger<SequentialCompositeJob>.Instance, [null!]));
    }

    /// <summary>
    /// Verifies child jobs execute sequentially in the configured order.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RunsChildJobsInConfiguredOrder()
    {
        var executionOrder = new List<string>();

        var jobs = new List<IJob>
        {
            new DelegateJob(_ =>
            {
                executionOrder.Add("activities");
                return Task.CompletedTask;
            }),
            new DelegateJob(_ =>
            {
                executionOrder.Add("detail");
                return Task.CompletedTask;
            }),
            new DelegateJob(_ =>
            {
                executionOrder.Add("streams");
                return Task.CompletedTask;
            })
        };

        var sut = new SequentialCompositeJob(NullLogger<SequentialCompositeJob>.Instance, jobs);

        await sut.ExecuteAsync(CancellationToken.None);

        Assert.Equal(new[] { "activities", "detail", "streams" }, executionOrder);
    }

    /// <summary>
    /// Verifies exceptions from child jobs are rethrown and stop subsequent execution.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RethrowsChildExceptionAndStopsRemainingJobs()
    {
        var executionOrder = new List<string>();

        var jobs = new List<IJob>
        {
            new DelegateJob(_ =>
            {
                executionOrder.Add("first");
                return Task.CompletedTask;
            }),
            new DelegateJob(_ =>
            {
                executionOrder.Add("second");
                throw new InvalidOperationException("failure");
            }),
            new DelegateJob(_ =>
            {
                executionOrder.Add("third");
                return Task.CompletedTask;
            })
        };

        var sut = new SequentialCompositeJob(NullLogger<SequentialCompositeJob>.Instance, jobs);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(CancellationToken.None));
        Assert.Equal(new[] { "first", "second" }, executionOrder);
    }

    /// <summary>
    /// Verifies cancellation from a child job is propagated.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RethrowsOperationCanceledException()
    {
        var jobs = new List<IJob>
        {
            new DelegateJob(_ => throw new OperationCanceledException())
        };

        var sut = new SequentialCompositeJob(NullLogger<SequentialCompositeJob>.Instance, jobs);

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(CancellationToken.None));
    }

    /// <summary>
    /// Implements a minimal delegated child job for composition tests.
    /// </summary>
    private sealed class DelegateJob : IJob
    {
        private readonly Func<CancellationToken, Task> execute;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateJob"/> class.
        /// </summary>
        /// <param name="execute">The delegate that executes job behavior.</param>
        public DelegateJob(Func<CancellationToken, Task> execute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        /// <summary>
        /// Executes the delegated job behavior.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel execution.</param>
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return this.execute(cancellationToken);
        }
    }
}
