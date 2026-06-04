using RookRun.Strava.Models;

namespace RookRun.Strava;

public interface IStravaActivities
{
    Task<IReadOnlyList<StravaActivity>> ListActivitiesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StravaActivity>> SearchActivitiesAsync(StravaActivityQuery query, CancellationToken cancellationToken = default);
}