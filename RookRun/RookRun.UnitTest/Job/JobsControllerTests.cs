using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RookRun.Api.Controllers;
using RookRun.Api.Jobs;
using RookRun.Contracts.Jobs;
using RookRun.Job;

namespace RookRun.UnitTest.Job;

/// <summary>
/// Unit tests for <see cref="JobsController"/>.
/// </summary>
public class JobsControllerTests
{
    /// <summary>
    /// Verifies that getting jobs returns the configured job catalog entries.
    /// </summary>
    [Fact]
    public void GetJobs_ReturnsOkWithCatalogValues()
    {
        var sut = CreateController(new ServiceCollection().BuildServiceProvider(), new Mock<IJobExecutionTracker>().Object);

        var result = sut.GetJobs();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var jobs = Assert.IsAssignableFrom<IEnumerable<JobInfoDto>>(okResult.Value);

        Assert.Equal(JobCatalog.Jobs.Count, jobs.Count());
    }

    /// <summary>
    /// Verifies that running a job returns a bad request when no request is supplied.
    /// </summary>
    [Fact]
    public void RunJob_ReturnsBadRequest_WhenRequestIsNull()
    {
        var sut = CreateController(new ServiceCollection().BuildServiceProvider(), new Mock<IJobExecutionTracker>().Object);

        var result = sut.RunJob(null!, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(badRequest.Value);

        Assert.False(response.Succeeded);
        Assert.Equal("A valid job name is required.", response.Message);
    }

    /// <summary>
    /// Verifies that running a job returns a bad request when the job name is blank.
    /// </summary>
    [Fact]
    public void RunJob_ReturnsBadRequest_WhenJobNameIsBlank()
    {
        var sut = CreateController(new ServiceCollection().BuildServiceProvider(), new Mock<IJobExecutionTracker>().Object);

        var result = sut.RunJob(new RunJobRequest { JobName = "   " }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(badRequest.Value);

        Assert.False(response.Succeeded);
        Assert.Equal("A valid job name is required.", response.Message);
    }

    /// <summary>
    /// Verifies that running a job returns not found when the requested job is not in the catalog.
    /// </summary>
    [Fact]
    public void RunJob_ReturnsNotFound_WhenJobIsNotRegistered()
    {
        var sut = CreateController(new ServiceCollection().BuildServiceProvider(), new Mock<IJobExecutionTracker>().Object);

        var result = sut.RunJob(new RunJobRequest { JobName = "UnknownJob" }, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(notFound.Value);

        Assert.False(response.Succeeded);
        Assert.Equal("UnknownJob", response.JobName);
        Assert.Equal("Job 'UnknownJob' is not registered.", response.Message);
    }

    /// <summary>
    /// Verifies that running a registered job queues background execution and returns accepted.
    /// </summary>
    [Fact]
    public async Task RunJob_ReturnsAccepted_WhenRegisteredJobIsQueued()
    {
        var jobName = nameof(SyncStravaActivitiesJob);
        var job = new Mock<IJob>();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        job
            .Setup(x => x.ExecuteAsync(CancellationToken.None))
            .Returns(Task.CompletedTask);

        var tracker = new Mock<IJobExecutionTracker>();
        tracker
            .Setup(x => x.TryStart(jobName))
            .Returns(true);
        tracker
            .Setup(x => x.Complete(jobName))
            .Callback(() => completion.TrySetResult(true));

        var services = new ServiceCollection();
        services.AddKeyedTransient<IJob>(jobName, (_, _) => job.Object);

        var sut = CreateController(services.BuildServiceProvider(), tracker.Object);

        var result = sut.RunJob(new RunJobRequest { JobName = jobName }, CancellationToken.None);

        var accepted = Assert.IsType<ObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(accepted.Value);

        Assert.True(response.Succeeded);
        Assert.Equal(jobName, response.JobName);
        Assert.Equal("Job started.", response.Message);
        Assert.Equal(202, accepted.StatusCode);

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));

        tracker.Verify(x => x.TryStart(jobName), Times.Once);
        job.Verify(x => x.ExecuteAsync(CancellationToken.None), Times.Once);
        tracker.Verify(x => x.Complete(jobName), Times.Once);
    }

    /// <summary>
    /// Verifies that running a registered job returns conflict when the same job is already running.
    /// </summary>
    [Fact]
    public void RunJob_ReturnsConflict_WhenJobIsAlreadyRunning()
    {
        var jobName = nameof(SyncStravaActivitiesJob);
        var job = new Mock<IJob>();
        var tracker = new Mock<IJobExecutionTracker>();

        tracker
            .Setup(x => x.TryStart(jobName))
            .Returns(false);

        var services = new ServiceCollection();
        services.AddKeyedTransient<IJob>(jobName, (_, _) => job.Object);

        var sut = CreateController(services.BuildServiceProvider(), tracker.Object);

        var result = sut.RunJob(new RunJobRequest { JobName = jobName }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(conflict.Value);

        Assert.Equal(409, conflict.StatusCode);
        Assert.False(response.Succeeded);
        Assert.Equal(jobName, response.JobName);
        Assert.Equal($"Job '{jobName}' is already running.", response.Message);

        job.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
        tracker.Verify(x => x.Complete(jobName), Times.Never);
    }

    /// <summary>
    /// Verifies that running a registered job still returns accepted when background execution fails.
    /// </summary>
    [Fact]
    public async Task RunJob_ReturnsAccepted_WhenBackgroundExecutionThrows()
    {
        var jobName = nameof(SyncStravaActivitiesJob);
        var job = new Mock<IJob>();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tracker = new Mock<IJobExecutionTracker>();

        tracker
            .Setup(x => x.TryStart(jobName))
            .Returns(true);
        tracker
            .Setup(x => x.Complete(jobName))
            .Callback(() => completion.TrySetResult(true));

        job
            .Setup(x => x.ExecuteAsync(CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("failure"));

        var services = new ServiceCollection();
        services.AddKeyedTransient<IJob>(jobName, (_, _) => job.Object);

        var sut = CreateController(services.BuildServiceProvider(), tracker.Object);

        var result = sut.RunJob(new RunJobRequest { JobName = jobName }, CancellationToken.None);

        var accepted = Assert.IsType<ObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(accepted.Value);

        Assert.True(response.Succeeded);
        Assert.Equal(jobName, response.JobName);
        Assert.Equal("Job started.", response.Message);
        Assert.Equal(202, accepted.StatusCode);

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
        tracker.Verify(x => x.Complete(jobName), Times.Once);
    }

    /// <summary>
    /// Creates a controller under test with a supplied service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider used by the controller.</param>
    /// <param name="tracker">The tracker used to guard duplicate in-process runs.</param>
    /// <returns>A configured controller instance.</returns>
    private static JobsController CreateController(IServiceProvider serviceProvider, IJobExecutionTracker tracker)
    {
        var logger = new Mock<ILogger<JobsController>>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        return new JobsController(scopeFactory, tracker, logger.Object);
    }
}
