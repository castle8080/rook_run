using Microsoft.Extensions.Options;
using RookRun.Garmin.Options;

namespace RookRun.Garmin;

public class GarminActivitiesFactory : IGarminActivitiesFactory
{
    private readonly GarminOptions _options;

    public GarminActivitiesFactory(IOptions<GarminOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IGarminActivities> CreateAsync()
    {
        var garminActivities = new GarminActivities(_options);
        await garminActivities.LoginAsync();
        return garminActivities;
    }
}
