namespace RookRun.Job;

/// <summary>
/// Tracks in-process job execution so the same job cannot run concurrently.
/// </summary>
public interface IJobExecutionTracker
{
    /// <summary>
    /// Attempts to mark the specified job as running.
    /// </summary>
    /// <param name="jobName">The job name to start tracking.</param>
    /// <returns><c>true</c> when tracking started; otherwise <c>false</c> if already running.</returns>
    bool TryStart(string jobName);

    /// <summary>
    /// Marks the specified job as no longer running.
    /// </summary>
    /// <param name="jobName">The job name to stop tracking.</param>
    void Complete(string jobName);
}
