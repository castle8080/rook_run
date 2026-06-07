using RookRun.Strava.Models;

namespace RookRun.Strava.Client;

public interface IStravaActivitiesClient
{
    Task<IReadOnlyList<StravaActivity>> ListActivitiesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StravaActivity>> SearchActivitiesAsync(StravaActivityQuery query, CancellationToken cancellationToken = default);
}