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
        var sut = CreateController(new ServiceCollection().BuildServiceProvider());

        var result = sut.GetJobs();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var jobs = Assert.IsAssignableFrom<IEnumerable<JobInfoDto>>(okResult.Value);

        Assert.Equal(JobCatalog.Jobs.Count, jobs.Count());
    }

    /// <summary>
    /// Verifies that running a job returns a bad request when no request is supplied.
    /// </summary>
    [Fact]
    public async Task RunJob_ReturnsBadRequest_WhenRequestIsNull()
    {
        var sut = CreateController(new ServiceCollection().BuildServiceProvider());

        var result = await sut.RunJob(null!, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(badRequest.Value);

        Assert.False(response.Succeeded);
        Assert.Equal("A valid job name is required.", response.Message);
    }

    /// <summary>
    /// Verifies that running a job returns a bad request when the job name is blank.
    /// </summary>
    [Fact]
    public async Task RunJob_ReturnsBadRequest_WhenJobNameIsBlank()
    {
        var sut = CreateController(new ServiceCollection().BuildServiceProvider());

        var result = await sut.RunJob(new RunJobRequest { JobName = "   " }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(badRequest.Value);

        Assert.False(response.Succeeded);
        Assert.Equal("A valid job name is required.", response.Message);
    }

    /// <summary>
    /// Verifies that running a job returns not found when the requested job is not in the catalog.
    /// </summary>
    [Fact]
    public async Task RunJob_ReturnsNotFound_WhenJobIsNotRegistered()
    {
        var sut = CreateController(new ServiceCollection().BuildServiceProvider());

        var result = await sut.RunJob(new RunJobRequest { JobName = "UnknownJob" }, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(notFound.Value);

        Assert.False(response.Succeeded);
        Assert.Equal("UnknownJob", response.JobName);
        Assert.Equal("Job 'UnknownJob' is not registered.", response.Message);
    }

    /// <summary>
    /// Verifies that running a registered job executes and returns success.
    /// </summary>
    [Fact]
    public async Task RunJob_ReturnsOk_WhenRegisteredJobExecutesSuccessfully()
    {
        var jobName = nameof(SyncStravaActivitiesJob);
        var cancellationToken = new CancellationTokenSource().Token;
        var job = new Mock<IJob>();

        job
            .Setup(x => x.ExecuteAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddKeyedTransient<IJob>(jobName, (_, _) => job.Object);

        var sut = CreateController(services.BuildServiceProvider());

        var result = await sut.RunJob(new RunJobRequest { JobName = jobName }, cancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(ok.Value);

        Assert.True(response.Succeeded);
        Assert.Equal(jobName, response.JobName);
        Assert.Equal("Job completed successfully.", response.Message);

        job.Verify(x => x.ExecuteAsync(cancellationToken), Times.Once);
    }

    /// <summary>
    /// Verifies that running a registered job returns server error when execution throws.
    /// </summary>
    [Fact]
    public async Task RunJob_ReturnsServerError_WhenJobExecutionThrows()
    {
        var jobName = nameof(SyncStravaActivitiesJob);
        var job = new Mock<IJob>();

        job
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("failure"));

        var services = new ServiceCollection();
        services.AddKeyedTransient<IJob>(jobName, (_, _) => job.Object);

        var sut = CreateController(services.BuildServiceProvider());

        var result = await sut.RunJob(new RunJobRequest { JobName = jobName }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var response = Assert.IsType<RunJobResponse>(objectResult.Value);

        Assert.Equal(500, objectResult.StatusCode);
        Assert.False(response.Succeeded);
        Assert.Equal(jobName, response.JobName);
        Assert.Equal("Job failed. Review API logs for details.", response.Message);
    }

    /// <summary>
    /// Creates a controller under test with a supplied service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider used by the controller.</param>
    /// <returns>A configured controller instance.</returns>
    private static JobsController CreateController(IServiceProvider serviceProvider)
    {
        var logger = new Mock<ILogger<JobsController>>();
        return new JobsController(serviceProvider, logger.Object);
    }
}
