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
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<JobsController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobsController"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve keyed job implementations.</param>
    /// <param name="logger">The logger used for execution telemetry.</param>
    public JobsController(IServiceProvider serviceProvider, ILogger<JobsController> logger)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RunJobResponse>> RunJob([FromBody] RunJobRequest request, CancellationToken cancellationToken)
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

        try
        {
            var job = this.serviceProvider.GetRequiredKeyedService<IJob>(request.JobName);
            await job.ExecuteAsync(cancellationToken);

            return Ok(new RunJobResponse
            {
                JobName = request.JobName,
                Succeeded = true,
                Message = "Job completed successfully.",
                CompletedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Job execution failed for {JobName}.", request.JobName);
            return StatusCode(StatusCodes.Status500InternalServerError, new RunJobResponse
            {
                JobName = request.JobName,
                Succeeded = false,
                Message = "Job failed. Review API logs for details.",
                CompletedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }
}
