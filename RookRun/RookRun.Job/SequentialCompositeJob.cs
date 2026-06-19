using Microsoft.Extensions.Logging;

namespace RookRun.Job;

/// <summary>
/// Executes a sequence of child jobs in order as a single composite workflow.
/// </summary>
public sealed class SequentialCompositeJob : IJob
{
    private readonly ILogger<SequentialCompositeJob> logger;
    private readonly IReadOnlyList<IJob> childJobs;

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialCompositeJob"/> class.
    /// </summary>
    /// <param name="logger">The logger used for composite execution telemetry.</param>
    /// <param name="childJobs">The ordered child jobs to execute sequentially.</param>
    public SequentialCompositeJob(ILogger<SequentialCompositeJob> logger, IReadOnlyList<IJob> childJobs)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.childJobs = childJobs ?? throw new ArgumentNullException(nameof(childJobs));

        if (this.childJobs.Count == 0)
        {
            throw new ArgumentException("At least one child job must be provided.", nameof(childJobs));
        }

        if (this.childJobs.Any(job => job is null))
        {
            throw new ArgumentException("Child jobs cannot contain null entries.", nameof(childJobs));
        }
    }

    /// <summary>
    /// Executes each configured child job in order and logs start/end boundaries for each one.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel execution.</param>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Starting sequential composite job with {JobCount} child jobs.", this.childJobs.Count);

        for (var index = 0; index < this.childJobs.Count; index++)
        {
            var childJob = this.childJobs[index];
            var childJobName = childJob.GetType().Name;
            var jobNumber = index + 1;

            this.logger.LogInformation(
                "Starting child job {JobNumber}/{JobCount}: {ChildJobName}.",
                jobNumber,
                this.childJobs.Count,
                childJobName);

            try
            {
                await childJob.ExecuteAsync(cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Child job {JobNumber}/{JobCount} cancelled: {ChildJobName}.",
                    jobNumber,
                    this.childJobs.Count,
                    childJobName);
                throw;
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Child job {JobNumber}/{JobCount} failed: {ChildJobName}.",
                    jobNumber,
                    this.childJobs.Count,
                    childJobName);
                throw;
            }

            this.logger.LogInformation(
                "Completed child job {JobNumber}/{JobCount}: {ChildJobName}.",
                jobNumber,
                this.childJobs.Count,
                childJobName);
        }

        this.logger.LogInformation("Sequential composite job completed successfully.");
    }
}
