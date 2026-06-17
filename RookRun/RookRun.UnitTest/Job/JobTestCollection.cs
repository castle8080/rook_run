namespace RookRun.UnitTest.Job;

/// <summary>
/// Defines a non-parallel test collection for tests that mutate process-global state.
/// </summary>
[CollectionDefinition("JobTests", DisableParallelization = true)]
public sealed class JobTestCollection
{
}
