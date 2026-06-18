using System.Collections.Concurrent;

namespace RookRun.Job;

/// <summary>
/// In-memory job tracker used to prevent duplicate concurrent runs per process.
/// </summary>
public sealed class InProcessJobExecutionTracker : IJobExecutionTracker
{
    private readonly ConcurrentDictionary<string, byte> _runningJobs =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryStart(string jobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        return _runningJobs.TryAdd(jobName, 0);
    }

    /// <inheritdoc />
    public void Complete(string jobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        _runningJobs.TryRemove(jobName, out _);
    }
}
