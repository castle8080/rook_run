using RookRun.Job.DependencyInjection;
using RookRun.ObjectStore.DependencyInjection;
using RookRun.Strava.DependencyInjection;

namespace RookRun.Api.Bootstrap;

static class AppServicesSetup
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddObjectStore(configuration)
            .AddStravaActivities(configuration)
            .AddJobs(configuration);
        return services;
    }
}
