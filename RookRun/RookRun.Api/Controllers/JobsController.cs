using Microsoft.AspNetCore.Mvc;
using RookRun.Api.Jobs;
using RookRun.Contracts.Jobs;
using RookRun.Job;

namespace RookRun.Api.Controllers;

/// <summary>
/// Exposes APIs for listing and running configured jobs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class JobsController : ControllerBase
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IJobExecutionTracker jobExecutionTracker;
    private readonly ILogger<JobsController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobsController"/> class.
    /// </summary>
    /// <param name="scopeFactory">The scope factory used to resolve keyed job implementations for background execution.</param>
    /// <param name="jobExecutionTracker">In-process tracker used to prevent duplicate runs of the same job.</param>
    /// <param name="logger">The logger used for execution telemetry.</param>
    public JobsController(
        IServiceScopeFactory scopeFactory,
        IJobExecutionTracker jobExecutionTracker,
        ILogger<JobsController> logger)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.jobExecutionTracker = jobExecutionTracker ?? throw new ArgumentNullException(nameof(jobExecutionTracker));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the list of runnable jobs.
    /// </summary>
    /// <returns>The configured jobs exposed to clients.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<JobInfoDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<JobInfoDto>> GetJobs()
    {
        return Ok(JobCatalog.Jobs.Values);
    }

    /// <summary>
    /// Runs a job by job name.
    /// </summary>
    /// <param name="request">The request containing the target job name.</param>
    /// <param name="cancellationToken">A token used to cancel execution.</param>
    /// <returns>A response describing whether the run completed successfully.</returns>
    [HttpPost("run")]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status500InternalServerError)]
    public ActionResult<RunJobResponse> RunJob([FromBody] RunJobRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.JobName))
        {
            return BadRequest(new RunJobResponse
            {
                Succeeded = false,
                Message = "A valid job name is required.",
                CompletedAtUtc = DateTimeOffset.UtcNow
            });
        }

        if (!JobCatalog.Jobs.ContainsKey(request.JobName))
        {
            return NotFound(new RunJobResponse
            {
                JobName = request.JobName,
                Succeeded = false,
                Message = $"Job '{request.JobName}' is not registered.",
                CompletedAtUtc = DateTimeOffset.UtcNow
            });
        }

        if (!this.jobExecutionTracker.TryStart(request.JobName))
        {
            this.logger.LogInformation("Job {JobName} is already running in this process.", request.JobName);
            return Conflict(new RunJobResponse
            {
                JobName = request.JobName,
                Succeeded = false,
                Message = $"Job '{request.JobName}' is already running.",
                CompletedAtUtc = DateTimeOffset.UtcNow
            });
        }

        try
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = this.scopeFactory.CreateScope();
                    var job = scope.ServiceProvider.GetRequiredKeyedService<IJob>(request.JobName);

                    this.logger.LogInformation("Background job {JobName} started.", request.JobName);
                    await job.ExecuteAsync(CancellationToken.None);
                    this.logger.LogInformation("Background job {JobName} completed successfully.", request.JobName);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Background job execution failed for {JobName}.", request.JobName);
                }
                finally
                {
                    this.jobExecutionTracker.Complete(request.JobName);
                }
            });

            return StatusCode(StatusCodes.Status202Accepted, new RunJobResponse
            {
                JobName = request.JobName,
                Succeeded = true,
                Message = "Job started.",
                CompletedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            this.jobExecutionTracker.Complete(request.JobName);
            this.logger.LogError(ex, "Failed to queue job execution for {JobName}.", request.JobName);

            return StatusCode(StatusCodes.Status500InternalServerError, new RunJobResponse
            {
                JobName = request.JobName,
                Succeeded = false,
                Message = "Job failed to start. Review API logs for details.",
                CompletedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }
}
