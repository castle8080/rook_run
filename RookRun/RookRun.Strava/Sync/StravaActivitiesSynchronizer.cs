using RookRun.Strava.Client;
using Microsoft.Extensions.Logging;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.Strava.Sync;

public sealed class StravaActivitiesSynchronizer
{
    private readonly IStravaActivitiesRepository stravaActivitiesRepository;
    private readonly IStravaActivitiesClient stravaActivitiesClient;
    private readonly ILogger<StravaActivitiesSynchronizer> logger;

    private const int MAX_DAYS_LOOK_BACK = 365 * 5;

    public StravaActivitiesSynchronizer(
        ILogger<StravaActivitiesSynchronizer> logger,
        IStravaActivitiesRepository stravaActivitiesRepository,
        IStravaActivitiesClient stravaActivitiesClient)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.stravaActivitiesRepository = stravaActivitiesRepository ?? throw new ArgumentNullException(nameof(stravaActivitiesRepository));
        this.stravaActivitiesClient = stravaActivitiesClient ?? throw new ArgumentNullException(nameof(stravaActivitiesClient));
    }

    public async Task<int> SyncAsync()
    {
        DateTime endDate = DateTimeOffset.UtcNow.DateTime;
        DateTime startDate = endDate - TimeSpan.FromDays(MAX_DAYS_LOOK_BACK);
        int pageNumber = 1;
        const int pageSize = 200;
        int totalSynced = 0;

        while (true)
        {
            // Get a page of activities
            var query = new StravaActivityQuery
            {
                After = startDate,
                Before = endDate,
                Page = pageNumber,
                PerPage = pageSize
            };

            this.logger.LogInformation("Syncing Strava activities, page {PageNumber}, page size {PageSize}", pageNumber, pageSize);

            var activities = await this.stravaActivitiesClient.SearchActivitiesAsync(query);

            if (activities.Count == 0)
            {
                break;
            }

            // Get the date range of the activities in this page
            var activityStartDates = activities
                .Select(a => a.StartDate)
                .OfType<DateTimeOffset>()
                .ToList();

            if (activityStartDates.Count == 0)
            {
                this.logger.LogWarning("Stopping Strava sync because page {PageNumber} returned {ActivityCount} activities without a StartDate.", pageNumber, activities.Count);
                break;
            }

            var minStartDate = activityStartDates.Min();
            var maxStartDate = activityStartDates.Max();

            this.logger.LogInformation("Activity date range: {MinStartDate} - {MaxStartDate}", minStartDate, maxStartDate);

            // Get the existing activities in the database that fall within this date range
            var savedMatchingActivities = await this.stravaActivitiesRepository.SearchAsync(minStartDate, maxStartDate);

            var savedMatchingActivityIds = savedMatchingActivities
                .Select(a => a.Id)
                .ToHashSet();

            // Figure out which activities from the API response are new and need to be saved.
            var newActivities = activities
                .Where(a => !savedMatchingActivityIds.Contains(a.Id))
                .ToList();

            this.logger.LogInformation("New activities to sync: {NewActivitiesCount}", newActivities.Count);

            if (newActivities.Count == 0)
            {
                // No new activities in this page, so we can stop syncing.
                break;
            }

            await this.stravaActivitiesRepository.SaveAllAsync(newActivities);
            totalSynced += newActivities.Count;
            pageNumber++;
        }

        return totalSynced;
    }
}
