using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Job.DependencyInjection;
using RookRun.Strava.DependencyInjection;

namespace RookRun.Bootstrap;

internal static class AppServicesSetup
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddStravaActivities(configuration.GetSection("Strava"))
            .AddJobs(configuration);
    }
}
