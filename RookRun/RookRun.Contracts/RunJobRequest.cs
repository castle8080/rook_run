namespace RookRun.Contracts.Jobs;

/// <summary>
/// Represents a request to run a job by name.
/// </summary>
public sealed class RunJobRequest
{
    /// <summary>
    /// Gets or sets the unique job name to execute.
    /// </summary>
    public string JobName { get; set; } = string.Empty;
}
