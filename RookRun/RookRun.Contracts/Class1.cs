namespace RookRun.Contracts;

/// <summary>
/// Describes a runnable job that can be displayed and selected by clients.
/// </summary>
/// <param name="Name">The unique job name used by the API to execute the job.</param>
/// <param name="DisplayName">The human-friendly job title shown in the UI.</param>
/// <param name="Description">A short summary of what the job does.</param>
public sealed record JobInfoDto(string Name, string DisplayName, string Description);
