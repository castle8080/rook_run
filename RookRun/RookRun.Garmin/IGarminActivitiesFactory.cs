namespace RookRun.Garmin;

public interface IGarminActivitiesFactory
{
    Task<IGarminActivities> CreateAsync();
}
