namespace RookRun.Contracts.Jobs;

/// <summary>
/// Represents the result of a job execution request.
/// </summary>
public sealed class RunJobResponse
{
    /// <summary>
    /// Gets or sets the requested job name.
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the run request completed successfully.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets a status message for the caller.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the request finished.
    /// </summary>
    public DateTimeOffset CompletedAtUtc { get; set; }
}
