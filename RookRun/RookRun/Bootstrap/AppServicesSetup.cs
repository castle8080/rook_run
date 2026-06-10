using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RookRun.GoogleHealth.DependencyInjection;
using RookRun.Job.DependencyInjection;
using RookRun.ObjectStore.DependencyInjection;
using RookRun.Strava.DependencyInjection;

namespace RookRun.Bootstrap;

internal static class AppServicesSetup
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddObjectStore(configuration)
            .AddStravaActivities(configuration)
            .AddGoogleHealth(configuration)
            .AddJobs(configuration);
    }
}
