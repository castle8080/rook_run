using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Job.DependencyInjection;
using RookRun.ObjectStore.DependencyInjection;
using RookRun.Strava.DependencyInjection;

namespace RookRun.Cli.Bootstrap;

internal static class AppServicesSetup
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddObjectStore(configuration)
            .AddStravaActivities(configuration)
            .AddJobs(configuration);
    }
}
