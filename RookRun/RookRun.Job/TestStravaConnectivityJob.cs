using Microsoft.Extensions.Logging;
using RookRun.Strava;
using System.Linq;

namespace RookRun.Job;

public class TestStravaConnectivityJob : IJob
{
    private readonly ILogger<TestStravaConnectivityJob> _logger;
    private readonly IStravaActivities _stravaActivities;

    public TestStravaConnectivityJob(
        ILogger<TestStravaConnectivityJob> logger,
        IStravaActivities stravaActivities)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stravaActivities = stravaActivities ?? throw new ArgumentNullException(nameof(stravaActivities));
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Strava connectivity test job.");

        var activities = await _stravaActivities.ListActivitiesAsync(cancellationToken);

        _logger.LogInformation("Successfully retrieved {ActivityCount} Strava activities.", activities.Count);

        foreach (var activity in activities.Take(5))
        {
            _logger.LogInformation(
                "Activity {ActivityId}: {ActivityName} ({ActivityStartDate})",
                activity.Id,
                activity.Name ?? "<unnamed>",
                activity.StartDate?.ToString("O") ?? "<no start date>");
        }
    }
}