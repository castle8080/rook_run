namespace RookRun.Strava.Models;

public sealed record StravaActivityQuery
{
    public DateTimeOffset? Before { get; init; }

    public DateTimeOffset? After { get; init; }

    public int? Page { get; init; }

    public int? PerPage { get; init; }
}